---
created_at: 2026-05-21
edited_at: 2026-05-21
creator: lead-dev
editor: lead-dev
ai_gen: true
---
# Bitcraft Local Setup

This page is the single source of truth for running Bitcraft locally on every supported host. Every container mode below builds from the same compose set; the only difference is which override files you stack on top of `docker-compose.yml`.

## Supported Hosts

| Host | Container runtime | GPU mode |
| --- | --- | --- |
| Windows 10/11 + Docker Desktop (WSL2 backend) | Docker Engine inside WSL2 | NVIDIA via `up:ai-gpu` (Docker Desktop ≥ 4.30 + recent NVIDIA driver) |
| macOS 13+ (Intel and Apple Silicon) + Docker Desktop | Docker Engine in the Linux VM | CPU only (`up:ai`) — no GPU passthrough |
| Linux (Ubuntu 22.04/24.04 reference) + Docker Engine | Docker Engine | NVIDIA via `up:ai-gpu` with NVIDIA Container Toolkit |
| Linux + NVIDIA discrete GPU | same as above | `up:ai-gpu` |
| NVIDIA Jetson Orin (JetPack 6.x, aarch64) | Docker Engine + `nvidia` runtime | `up:ai-jetson` only — uses the dustynv image |

## Prerequisites

### All platforms
1. Install **Docker** (Docker Desktop on Windows/macOS, Docker Engine + Compose v2 plugin on Linux).
2. Install **[Task](https://taskfile.dev/installation/)** (recommended) — gives you `task up:dev`, `task validate`, etc.
3. Clone the repository and `cp .env.example .env` (edit as needed).

### Windows + Docker Desktop
- Enable **WSL2 integration** for the distribution you plan to run `task` from.
- For GPU mode: install a current NVIDIA Studio/Game Ready driver (Docker Desktop talks to it via WSL2 — no separate Linux driver inside WSL).
- LF normalization: this repo expects `*.sh` / `*.yml` to be `LF`-only. If you see "Bad interpreter" errors, run `git config core.autocrlf false` and re-clone.

### macOS + Docker Desktop
- Apple Silicon: nothing extra; images that lack `arm64` variants are pulled with emulation (slow). Pin `platform: linux/amd64` only if you hit a runtime bug.
- Hot reload: enable **VirtioFS** in Docker Desktop ➜ General. If HMR still misses changes, set `VITE_USE_POLLING=true` in `.env`.

### Linux + Docker Engine
- Install Compose v2 (`docker compose version` must succeed; the legacy `docker-compose` script is **not** supported).
- Optional: add your user to the `docker` group so you don't need `sudo`.

### Linux + NVIDIA GPU
- Install the **[NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html)**.
- Verify: `docker run --rm --gpus all nvidia/cuda:12.4.0-base-ubuntu22.04 nvidia-smi`.

### NVIDIA Jetson Orin (JetPack 6.x)
- JetPack 6.x (L4T r36.4+) flashed and booted.
- `nvidia-container-toolkit` and `nvidia-docker2` packages installed; default Docker runtime set to `nvidia` (or use the `runtime: nvidia` already wired in the compose file).
- Internet access for the first run (the dustynv image is several GB).

## Day-to-day Commands (Taskfile)

| Task | What it does |
| --- | --- |
| `task up:core` | Backend + Postgres + sidecar + tutor-studio (no AI, production-style build). |
| `task up:dev` | Same plus tutor-studio mounted from the host with HMR on http://localhost:5173. |
| `task up:ai` | Adds local Ollama on http://localhost:11434 (CPU). Pulls the embed and chat models on first run. |
| `task up:ai-gpu` | `up:ai` plus NVIDIA discrete GPU acceleration. |
| `task up:ai-jetson` | Jetson Orin variant (aarch64-only). |
| `task up:prod` | `up:core` plus nginx-served tutor-studio on http://localhost:8080. |
| `task down` | Stop every variant. |
| `task reset` | Stop every variant **and delete every named volume** (Postgres + Ollama cache). Prompts for confirmation. |
| `task status` | `docker compose ps` across every variant. |
| `task logs` | Tail logs from the core+AI stack. |
| `task validate` | `docker compose config --quiet` over every documented combination. CI-friendly. |

### Without Task installed

Use the menu launchers:
- **Windows / PowerShell:** `./run.ps1`
- **Linux / macOS / WSL:** `./run.sh`

Or the raw compose commands below.

## Raw Compose Commands

```bash
# Core only
docker compose -f docker-compose.yml up --build -d

# Dev (HMR)
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build -d

# AI (CPU)
docker compose -f docker-compose.yml -f docker-compose.ai.yml up --build -d

# AI + GPU (Linux NVIDIA, Windows Docker Desktop NVIDIA)
docker compose -f docker-compose.yml -f docker-compose.ai.yml -f docker-compose.ai.gpu.yml up --build -d

# AI + Jetson (aarch64 only)
docker compose -f docker-compose.yml -f docker-compose.ai.yml -f docker-compose.ai.jetson.yml up --build -d

# Prod (nginx)
docker compose -f docker-compose.yml -f docker-compose.prod.yml up --build -d

# Stop / reset
docker compose -f docker-compose.yml -f docker-compose.ai.yml -f docker-compose.ai.gpu.yml -f docker-compose.ai.jetson.yml down
docker compose -f docker-compose.yml -f docker-compose.ai.yml -f docker-compose.ai.gpu.yml -f docker-compose.ai.jetson.yml down -v
```

## Model Defaults

| Variable | Default | Purpose | Notes |
| --- | --- | --- | --- |
| `OLLAMA_CHAT_MODEL` | `llama3.2:3b` | Default chat model pulled on `up:ai*`. | Jetson Orin Nano (8 GB): set to `llama3.2:1b`. |
| `OLLAMA_EMBED_MODEL` | `qwen3-embedding:0.6b` | Vector embedding model. | Bitcraft expects the dimension below to match the model. |
| `OLLAMA_EMBED_DIMENSIONS` | `1024` | Pgvector column dimension. | Changing this requires re-embedding all Knowledge. |
| `OLLAMA_BASE_URL` | `http://host.docker.internal:11434` | Backend → Ollama address. | Override only if you point at a remote Ollama. |
| `VITE_USE_POLLING` | `false` | Vite file-watch mode in the dev container. | Set to `true` on WSL2 and some macOS VirtioFS setups. |

The full reference is in [`.env.example`](../.env.example).

## Hot-reload Caveats

- **WSL2:** inotify events from a Windows-mounted bind do not reach the dev container. Either work inside the WSL filesystem (recommended) or set `VITE_USE_POLLING=true`.
- **macOS VirtioFS:** usually fine. If HMR misses, toggle `VITE_USE_POLLING=true`.
- **Linux:** native inotify works; no extra config.
- The dev override uses an anonymous `node_modules` volume so the host's tree (often the wrong OS/arch) never overwrites the container install. Re-run `task rebuild -- tutor-studio` after editing `tutor-studio/package.json`.

## GPU Verification

After `task up:ai-gpu`:

```bash
docker exec -it ollama nvidia-smi
docker exec -it ollama ollama list
```

`nvidia-smi` should show your GPU; the first chat request should report `done_reason: gpu` in `docker logs ollama`.

## Jetson Notes

- The compose pin is `dustynv/ollama:0.6.8-r36.4-cu126-22.04` (JetPack 6.x). Bump the tag only after confirming a matching L4T release.
- `network_mode: host` is required so the Jetson's hardware NVDEC/encoders are visible; the backend reaches it via `host.docker.internal` + the `host-gateway` shim.
- First boot pulls both `OLLAMA_CHAT_MODEL` and `OLLAMA_EMBED_MODEL` — budget bandwidth and disk (~2 GB chat, ~700 MB embed).
- The default chat model on Jetson is the smaller `llama3.2:1b`; override `OLLAMA_CHAT_MODEL` if you have an Orin AGX.

## Troubleshooting

- **`backend` won't become healthy.** Check `docker logs bitcraft-backend`. If migrator failed, look at `docker logs bitcraft-migrator` (DB not reachable, or migration error).
- **`tutor-studio` exits immediately on `up:dev`.** The bind-mount needs the host repo cloned with LF line endings. On Windows, run `git config core.autocrlf false` and re-clone, or fix `tutor-studio/node_modules` by running `task rebuild -- tutor-studio`.
- **Sidecar healthcheck fails.** The Unix Domain Socket lives in the `benchmark_sidecar_socket` named volume. After a `task reset`, the socket recreates automatically on first `up`.
- **Port collisions.** Defaults: backend `8000`, tutor-studio dev `5173`, prod `8080`, Postgres `5433`, Ollama `11434`. Override in your `.env` if any is taken.
- **`docker compose config` complains about `version:`.** None of our files set it (Compose v2 ignores top-level `version`). If you have an older Docker Compose v1, upgrade.
- **Pgvector volume reset.** `task reset` is the only command that drops the database. There is no soft-reset.

## Validating a Change

Before opening a PR that touches compose files, run:

```bash
task validate
```

This runs `docker compose config --quiet` for every documented file combination and fails fast on syntax or interpolation errors.
