import { test } from 'node:test';
import assert from 'node:assert/strict';
import { runTurn } from './runtime.mjs';

const tool = name => ({ function: { name, description: name, parameters: { type: 'object', properties: {}, additionalProperties: false } } });
const call = (id, name) => ({ id, type: 'function', function: { name, arguments: '{}' } });
test('Pi owns sequential tool execution and passes results to the next model turn', async () => {
  const executed = [];
  let rounds = 0;
  const history = await runTurn({ instructions: 'Only propose', tools: [tool('list_catalog'), tool('search_slots')],
    history: [{ role: 'user', content: '帮我预约' }] }, {
    async model(messages, delta) {
      if (rounds++ === 0) return { content: '', tool_calls: [call('a', 'list_catalog'), call('b', 'search_slots')] };
      assert.deepEqual(messages.filter(m => m.role === 'tool').map(m => m.tool_call_id), ['a', 'b']);
      delta('请核对'); delta('候选');
      return { content: '请核对候选' };
    },
    async tool(name) { executed.push(name); return { result: name }; },
  });
  assert.equal(rounds, 2);
  assert.deepEqual(executed, ['list_catalog', 'search_slots']);
  assert.equal(history.at(-1).content, '请核对候选');
});

test('Pi recovers from an assistant tail using a new trusted conflict event', async () => {
  const history = await runTurn({ instructions: 'Keep constraints', tools: [], recovering: true,
    history: [{ role: 'user', content: '预约' }, { role: 'assistant', content: '候选' }] }, {
    async model(messages) {
      assert.equal(messages.at(-1).role, 'user');
      assert.match(messages.at(-1).content, /冲突/);
      return { content: '重新查询' };
    }, tool: () => { throw new Error('Unexpected tool'); },
  });
  assert.equal(history.at(-1).content, '重新查询');
});

test('Pi stops when the backend model transport fails', async () => {
  await assert.rejects(() => runTurn({ instructions: '', tools: [], history: [{ role: 'user', content: '预约' }] }, {
    model: () => { throw new Error('secret must not be forwarded'); }, tool: () => {},
  }), /Incomplete agent turn/);
});
