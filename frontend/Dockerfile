# Development image. Compose bind mounts the source and Next.js hot reloads,
# so this stage exists mainly to install dependencies into the image.
FROM node:22-alpine

WORKDIR /app

# Install first, with only the manifests copied, so a source edit does not
# reinstall every package.
COPY package.json package-lock.json ./
# ci, not install: the lockfile is committed, so every machine and CI install
# exactly the same tree. Changed package.json? Regenerate the lockfile.
RUN npm ci

COPY . .

EXPOSE 3000

CMD ["npm", "run", "dev"]
