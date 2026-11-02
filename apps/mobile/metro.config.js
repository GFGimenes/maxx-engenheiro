// https://docs.expo.dev/guides/customizing-metro/
const http = require("node:http");
const { getDefaultConfig } = require("expo/metro-config");

const config = getDefaultConfig(__dirname);

// SÓ DESENVOLVIMENTO. Em `expo start`, o servidor do Metro repassa /api/* para a API
// local, e a web continua numa origem só (ADR 0001), sem CORS. O middleware vive no
// servidor de desenvolvimento: `expo export` não sobe servidor, então o proxy não existe
// no build exportado. Lá quem faz esse papel é o Caddy.
const apiUrl = process.env.MAXX_DEV_API_URL ?? "http://localhost:5028";

function proxyToApi(req, res) {
  const target = new URL(req.url, apiUrl);
  const upstream = http.request(
    target,
    { method: req.method, headers: { ...req.headers, host: target.host } },
    (response) => {
      res.writeHead(response.statusCode ?? 502, response.headers);
      response.pipe(res);
    },
  );
  upstream.on("error", () => {
    res.writeHead(502, { "content-type": "application/problem+json" });
    res.end(
      JSON.stringify({
        title: `API local fora do ar em ${apiUrl}.`,
        status: 502,
      }),
    );
  });
  req.pipe(upstream);
}

const expoEnhanceMiddleware = config.server?.enhanceMiddleware;

config.server = {
  ...config.server,
  enhanceMiddleware: (middleware, server) => {
    const next = expoEnhanceMiddleware
      ? expoEnhanceMiddleware(middleware, server)
      : middleware;
    return (req, res, fallthrough) =>
      req.url?.startsWith("/api/")
        ? proxyToApi(req, res)
        : next(req, res, fallthrough);
  },
};

module.exports = config;
