import { Agent } from '@earendil-works/pi-agent-core';
import { createAssistantMessageEventStream } from '@earendil-works/pi-ai';

const model = { id: 'clinicflow-model', name: 'ClinicFlow backend model', provider: 'clinicflow',
  api: 'openai-completions', baseUrl: '', reasoning: false, input: ['text'],
  contextWindow: 128000, maxTokens: 1600,
  cost: { input: 0, output: 0, cacheRead: 0, cacheWrite: 0 } };
const usage = () => ({ input: 0, output: 0, cacheRead: 0, cacheWrite: 0, totalTokens: 0,
  cost: { input: 0, output: 0, cacheRead: 0, cacheWrite: 0, total: 0 } });
function assistant(message) {
  const content = [];
  if (message.content) content.push({ type: 'text', text: message.content });
  for (const call of message.tool_calls ?? []) content.push({ type: 'toolCall', id: call.id,
    name: call.function.name, arguments: JSON.parse(call.function.arguments) });
  return { role: 'assistant', content, api: model.api, provider: model.provider, model: model.id,
    usage: usage(), stopReason: message.tool_calls?.length ? 'toolUse' : 'stop', timestamp: Date.now() };
}
function fromChat(messages) {
  const names = new Map();
  return messages.map(message => {
    if (message.role === 'assistant') {
      for (const call of message.tool_calls ?? []) names.set(call.id, call.function.name);
      return assistant(message);
    }
    if (message.role === 'tool') return { role: 'toolResult', toolCallId: message.tool_call_id,
      toolName: names.get(message.tool_call_id) ?? 'unknown', content: [{ type: 'text', text: message.content }],
      isError: false, timestamp: Date.now() };
    return { role: 'user', content: message.content, timestamp: Date.now() };
  });
}
function toChat(messages) {
  return messages.filter(m => ['user', 'assistant', 'toolResult'].includes(m.role)).map(m => {
    if (m.role === 'user') return { role: 'user', content: typeof m.content === 'string' ? m.content : m.content.filter(c => c.type === 'text').map(c => c.text).join('') };
    if (m.role === 'toolResult') return { role: 'tool', tool_call_id: m.toolCallId, content: m.content.filter(c => c.type === 'text').map(c => c.text).join('') };
    const calls = m.content.filter(c => c.type === 'toolCall');
    return { role: 'assistant', content: m.content.filter(c => c.type === 'text').map(c => c.text).join(''),
      ...(calls.length ? { tool_calls: calls.map(c => ({ id: c.id, type: 'function', function: { name: c.name, arguments: JSON.stringify(c.arguments) } })) } : {}) };
  });
}

// Pi owns the full conversational/tool loop. The custom transport delegates model
// I/O and read-only domain tools to ASP.NET over private process pipes: no key,
// database credentials or network listener are required in this runtime.
export async function runTurn(input, host) {
  let requests = 0;
  const tools = input.tools.map(({ function: tool }) => ({ name: tool.name, label: tool.name,
    description: tool.description, parameters: tool.parameters, executionMode: 'sequential',
    execute: async (_id, args) => ({ content: [{ type: 'text', text: JSON.stringify(await host.tool(tool.name, args)) }], details: {} }) }));
  const agent = new Agent({
    initialState: { systemPrompt: input.instructions, model, tools, messages: fromChat(input.history), thinkingLevel: 'off' },
    toolExecution: 'sequential',
    streamFn: (_model, context) => {
      const stream = createAssistantMessageEventStream();
      const partial = assistant({ content: '' });
      partial.content = [{ type: 'text', text: '' }];
      queueMicrotask(async () => {
        stream.push({ type: 'start', partial });
        try {
          if (++requests > 10) throw new Error('Model turn limit');
          const answer = await host.model(toChat(context.messages), text => {
            partial.content[0].text += text;
            stream.push({ type: 'text_delta', contentIndex: 0, delta: text, partial });
          });
          const message = assistant(answer);
          stream.push({ type: 'done', reason: message.stopReason, message });
        } catch {
          partial.stopReason = 'error';
          partial.errorMessage = 'Agent runtime model request failed';
          stream.push({ type: 'error', reason: 'error', error: partial });
        }
      });
      return stream;
    },
  });
  if (input.recovering) await agent.prompt('原候选已冲突，请按系统事件重新查询，保持原约束。');
  else await agent.continue();
  const last = agent.state.messages.at(-1);
  if (last?.role !== 'assistant' || last.stopReason !== 'stop') throw new Error('Incomplete agent turn');
  return toChat(agent.state.messages);
}
