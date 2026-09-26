import { createInterface } from 'node:readline';
import { runTurn } from './runtime.mjs';

const pending = new Map();
let nextId = 0, started = false;
const write = value => process.stdout.write(JSON.stringify(value) + '\n');
function request(type, payload, onDelta) {
  const id = ++nextId;
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject, onDelta });
    write({ type, id, ...payload });
  });
}
const lines = createInterface({ input: process.stdin, crlfDelay: Infinity });
lines.on('line', line => {
  try {
    const frame = JSON.parse(line);
    if (frame.type === 'init' && !started) {
      started = true;
      runTurn(frame, {
        model: (history, onDelta) => request('model', { history }, onDelta),
        tool: (name, args) => request('tool', { name, args }),
      }).then(history => write({ type: 'done', history }), () => write({ type: 'error' }))
        .finally(() => { lines.close(); process.stdin.destroy(); });
      return;
    }
    const item = pending.get(frame.id);
    if (!item) return;
    if (frame.type === 'delta') { item.onDelta?.(frame.text); return; }
    pending.delete(frame.id);
    if (frame.type === 'response') item.resolve(frame.value);
    else item.reject(new Error('Host request failed'));
  } catch { write({ type: 'error' }); process.exitCode = 1; lines.close(); process.stdin.destroy(); }
});
lines.on('close', () => {
  for (const item of pending.values()) item.reject(new Error('Host disconnected'));
  pending.clear();
});
