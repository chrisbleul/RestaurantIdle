# syntax=docker/dockerfile:1
# Aus dem Wurzelverzeichnis bauen: docker build -f apps/api/Dockerfile .

FROM node:22-alpine AS build
WORKDIR /app

# Erst nur die Manifeste -- so bleibt die Installationsschicht im Cache,
# solange sich keine Abhaengigkeit aendert.
COPY apps/api/package.json apps/api/package-lock.json ./
RUN npm ci

COPY apps/api/tsconfig.json ./
COPY apps/api/src src
COPY apps/api/migrations migrations
RUN npm run build

# Entwicklungsabhaengigkeiten aus dem Baum werfen, bevor er ins Laufzeitbild geht.
RUN npm prune --omit=dev

FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production

ARG GIT_REVISION=dev
ENV GIT_REVISION=$GIT_REVISION

COPY --from=build /app/node_modules node_modules
COPY --from=build /app/dist dist
COPY --from=build /app/package.json ./
COPY --from=build /app/migrations migrations

# Nicht als root laufen. Das Bild bringt den Nutzer "node" bereits mit.
USER node

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD node -e "fetch('http://localhost:3000/api/health').then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))"

EXPOSE 3000
CMD ["node", "dist/server.js"]
