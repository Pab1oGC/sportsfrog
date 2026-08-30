# =============================================================================
# Frontend image, in two stages:
#
#   build     compiles the SPA with Vite.
#   runtime   nginx, serving the built files and reverse-proxying /api and
#             /sportfrog to the API and to object storage — see nginx.conf
#             for why: the same same-origin trick the dev proxy in
#             vite.config.js does, so production never needs CORS either,
#             and so signed object-storage links stay reachable from
#             outside Docker.
#
# The build context is this folder (web/):
#   docker build -t sportfrog-web .
# =============================================================================

FROM node:22-alpine AS build
WORKDIR /src

# package*.json first, so the install layer is reused while dependencies
# don't change — the same reasoning as api/Dockerfile's restore step.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# VITE_API_URL is left unset on purpose. Empty is what makes axios request
# relative paths, which is exactly what nginx.conf's /api/ proxy expects —
# see .env.example for the full reasoning. Baking in an absolute API URL
# here would defeat the same-origin setup this image exists to provide.
RUN npm run build

# -----------------------------------------------------------------------------
FROM nginx:1.27-alpine AS runtime

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist /usr/share/nginx/html

EXPOSE 80
