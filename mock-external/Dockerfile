FROM node:24.13.1-alpine
WORKDIR /app
COPY . .
RUN mkdir /data && chown node:node /data
USER node
CMD ["node", "server.mjs"]
