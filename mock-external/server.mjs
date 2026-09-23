import { createServer } from 'node:http';
createServer((req,res) => { res.setHeader('Content-Type','application/json'); res.end(JSON.stringify({status:'ready'})); }).listen(5090, '0.0.0.0');
