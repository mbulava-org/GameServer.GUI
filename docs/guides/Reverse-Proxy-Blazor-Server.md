# Reverse Proxy Configuration for GameServer.Web (Blazor Server)

`GameServer.Web` is a **Blazor Server** application. Its interactivity depends on a
persistent SignalR **circuit** established over the `/_blazor` endpoint (WebSocket
transport). If the reverse proxy does not correctly forward WebSocket upgrades and
`X-Forwarded-*` headers, the circuit is terminated and **all interactive controls stop
working** (buttons, grids, list selections) even though pages still render.

## What the application already does

The app is configured in [`Program.cs`](../../src/GameServer.Web/Program.cs) to work behind a proxy:

- `app.UseForwardedHeaders()` honors `X-Forwarded-For` / `X-Forwarded-Proto` / `X-Forwarded-Host`
  so the circuit negotiates against the external scheme/host the browser used.
- `app.UseWebSockets()` enables the WebSocket transport for `/_blazor`.
- HTTPS redirection is only enabled when an HTTPS port is actually configured, so TLS can be
  terminated at the proxy without breaking the circuit.

The container listens on **HTTP port 8080** (see the `Dockerfile`). TLS is expected to be
terminated at the proxy.

## Requirements the proxy MUST satisfy

1. Forward WebSocket upgrades (`Upgrade` / `Connection: upgrade`) for `/_blazor` (and any
   other SignalR hubs the app uses).
2. Send `X-Forwarded-Proto` and `X-Forwarded-Host` so the app knows the external scheme/host.
3. Use a sufficiently long read/idle timeout so the persistent circuit is not dropped.

---

## Traefik (v3) — Docker labels

Traefik natively supports WebSockets over the standard HTTP router (no special entrypoint is
required); you only need to route traffic to the container's HTTP port and pass forwarded
headers. Attach these labels to the `GameServer.Web` service.

```yaml
services:
  gameserver-web:
	image: gameserver-web:latest
	# The app serves plain HTTP inside the container; TLS is terminated by Traefik.
	environment:
	  - ASPNETCORE_ENVIRONMENT=Production
	  - ASPNETCORE_HTTP_PORTS=8080
	networks:
	  - web
	labels:
	  - "traefik.enable=true"

	  # ---- Router (HTTPS entrypoint, TLS terminated at Traefik) ----
	  - "traefik.http.routers.gameserver-web.rule=Host(`gameserver.example.com`)"
	  - "traefik.http.routers.gameserver-web.entrypoints=websecure"
	  - "traefik.http.routers.gameserver-web.tls=true"
	  - "traefik.http.routers.gameserver-web.tls.certresolver=letsencrypt"
	  - "traefik.http.routers.gameserver-web.service=gameserver-web"

	  # ---- Service (points at the container's HTTP port) ----
	  - "traefik.http.services.gameserver-web.loadbalancer.server.port=8080"

	  # ---- Forwarded headers / sticky circuit ----
	  # Traefik forwards X-Forwarded-* automatically. Add sticky sessions only if you run
	  # more than one replica of the web app (a Blazor circuit is bound to one instance).
	  - "traefik.http.services.gameserver-web.loadbalancer.sticky.cookie=true"
	  - "traefik.http.services.gameserver-web.loadbalancer.sticky.cookie.name=gs_affinity"
	  - "traefik.http.services.gameserver-web.loadbalancer.sticky.cookie.httponly=true"
	  - "traefik.http.services.gameserver-web.loadbalancer.sticky.cookie.secure=true"

networks:
  web:
	external: true
```

### Notes for Traefik

- **WebSockets just work.** Traefik proxies WebSockets transparently on the same HTTP router;
  there is no separate label to "enable" them.
- **Sticky sessions** matter only if you scale the web app to multiple replicas. A Blazor
  Server circuit lives in the memory of a single instance, so requests for a given user must
  return to the same container. The single-replica case needs no stickiness.
- **Timeouts.** If circuits drop after idle periods, raise the transport timeout on the
  entrypoint, e.g. (static config):

  ```yaml
  entryPoints:
	websecure:
	  address: ":443"
	  transport:
		respondingTimeouts:
		  idleTimeout: 3600s
  ```

---

## nginx — equivalent configuration

```nginx
map $http_upgrade $connection_upgrade {
	default upgrade;
	''      close;
}

server {
	listen 443 ssl;
	server_name gameserver.example.com;

	# ssl_certificate / ssl_certificate_key ...

	location / {
		proxy_pass         http://gameserver-web:8080;
		proxy_http_version 1.1;

		# WebSocket upgrade for the Blazor '/_blazor' circuit
		proxy_set_header   Upgrade    $http_upgrade;
		proxy_set_header   Connection $connection_upgrade;

		# Forwarded headers so the app negotiates the correct external scheme/host
		proxy_set_header   Host              $host;
		proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
		proxy_set_header   X-Forwarded-Proto $scheme;
		proxy_set_header   X-Forwarded-Host  $host;

		# Keep the persistent circuit alive
		proxy_read_timeout  3600s;
		proxy_send_timeout  3600s;
	}
}
```

---

## Troubleshooting

| Symptom | Likely cause |
| --- | --- |
| All interactive controls dead app-wide; console shows the `/_blazor` WebSocket failing | Proxy not forwarding `Upgrade`/`Connection` headers |
| `Failed to determine the https port for redirect` in container logs | HTTPS redirection enabled without an HTTPS port — terminate TLS at the proxy and leave the container on HTTP |
| Circuit drops after a period of inactivity | Proxy read/idle timeout too short — raise it as shown above |
| Works with one replica, breaks when scaled out | Missing sticky sessions — Blazor circuits are instance-bound |
