FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:alpine AS runtime
ENV API_ORIGIN=http://api:8080
ENV NGINX_ENVSUBST_FILTER=API_ORIGIN

COPY nginx.conf /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist/job-fit-web/browser /usr/share/nginx/html

EXPOSE 80
