#!/usr/bin/env bash

set -euo pipefail

PROJECT="bitcraft"

BASE_FILE="docker-compose.yml"
AI_FILE="docker-compose.ai.yml"
GPU_FILE="docker-compose.ai.gpu.yml"

BASE_ARGS=(-f "$BASE_FILE")
AI_ARGS=(-f "$AI_FILE")
GPU_ARGS=(-f "$GPU_FILE")

RED="\033[0;31m"
GREEN="\033[0;32m"
YELLOW="\033[1;33m"
BLUE="\033[0;34m"
CYAN="\033[0;36m"
NC="\033[0m"

print_header() {
  clear
  echo -e "${CYAN}======================================${NC}"
  echo -e "${CYAN}          BITCRAFT LAUNCHER           ${NC}"
  echo -e "${CYAN}======================================${NC}"
  echo
}

info() {
  echo -e "${BLUE}[INFO]${NC} $1"
}

success() {
  echo -e "${GREEN}[OK]${NC} $1"
}

warn() {
  echo -e "${YELLOW}[WARN]${NC} $1"
}

error() {
  echo -e "${RED}[ERROR]${NC} $1"
}

pause() {
  echo
  read -rp "Press Enter to continue..."
}

check_docker() {
  if ! command -v docker >/dev/null 2>&1; then
    error "Docker is not installed or not in PATH."
    exit 1
  fi

  if ! docker info >/dev/null 2>&1; then
    error "Docker daemon is not running or you do not have permission to use it."
    exit 1
  fi

  if ! docker compose version >/dev/null 2>&1; then
    error "Docker Compose plugin is not available. 'docker compose' failed."
    exit 1
  fi
}

has_nvidia_gpu() {
  if command -v nvidia-smi >/dev/null 2>&1; then
    return 0
  fi
  return 1
}

compose_up() {
  local mode="$1"
  shift
  local files=("$@")

  info "Starting stack: $mode"
  docker compose "${files[@]}" -p "$PROJECT" up --build -d
  success "$mode started."
}

compose_down_all() {
  info "Stopping stack..."
  docker compose "${BASE_ARGS[@]}" "${AI_ARGS[@]}" "${GPU_ARGS[@]}" -p "$PROJECT" down || true
  success "Stack stopped."
}

compose_reset_all() {
  warn "This will stop containers and delete volumes."
  read -rp "Are you sure? (y/N): " confirm
  if [[ "${confirm:-N}" =~ ^[Yy]$ ]]; then
    docker compose "${BASE_ARGS[@]}" "${AI_ARGS[@]}" "${GPU_ARGS[@]}" -p "$PROJECT" down -v || true
    success "Stack reset completed."
  else
    info "Reset cancelled."
  fi
}

show_logs() {
  echo
  echo "1) All services"
  echo "2) Backend"
  echo "3) Admin"
  echo "4) Database"
  echo "5) Ollama"
  echo "6) Back"
  echo
  read -rp "Choose logs option: " log_choice

  case "$log_choice" in
    1) docker compose "${BASE_ARGS[@]}" "${AI_ARGS[@]}" -p "$PROJECT" logs -f ;;
    2) docker compose "${BASE_ARGS[@]}" "${AI_ARGS[@]}" -p "$PROJECT" logs -f backend ;;
    3) docker compose "${BASE_ARGS[@]}" "${AI_ARGS[@]}" -p "$PROJECT" logs -f admin ;;
    4) docker compose "${BASE_ARGS[@]}" -p "$PROJECT" logs -f db ;;
    5) docker compose "${BASE_ARGS[@]}" "${AI_ARGS[@]}" -p "$PROJECT" logs -f ollama ;;
    6) return ;;
    *) warn "Invalid option." ;;
  esac
}

rebuild_service() {
  echo
  echo "Available services:"
  echo "1) backend"
  echo "2) admin"
  echo "3) db"
  echo "4) ollama"
  echo "5) Back"
  echo
  read -rp "Choose service: " service_choice

  local service=""
  local files=("${BASE_ARGS[@]}")

  case "$service_choice" in
    1) service="backend"; files=("${BASE_ARGS[@]}" "${AI_ARGS[@]}") ;;
    2) service="admin"; files=("${BASE_ARGS[@]}") ;;
    3) service="db"; files=("${BASE_ARGS[@]}") ;;
    4) service="ollama"; files=("${BASE_ARGS[@]}" "${AI_ARGS[@]}") ;;
    5) return ;;
    *) warn "Invalid option."; return ;;
  esac

  info "Rebuilding service: $service"
  docker compose "${files[@]}" -p "$PROJECT" up --build -d "$service"
  success "Service '$service' rebuilt."
}

show_status() {
  info "Current container status:"
  docker compose "${BASE_ARGS[@]}" "${AI_ARGS[@]}" "${GPU_ARGS[@]}" -p "$PROJECT" ps || true
}

run_core() {
  compose_up "Core Stack" "${BASE_ARGS[@]}"
}

run_core_ai() {
  if [[ ! -f "$AI_FILE" ]]; then
    error "$AI_FILE not found."
    return
  fi
  compose_up "Core + AI Stack" "${BASE_ARGS[@]}" "${AI_ARGS[@]}"
}

run_core_ai_gpu() {
  if [[ ! -f "$AI_FILE" ]]; then
    error "$AI_FILE not found."
    return
  fi

  if [[ ! -f "$GPU_FILE" ]]; then
    error "$GPU_FILE not found."
    return
  fi

  if has_nvidia_gpu; then
    info "NVIDIA GPU detected."
  else
    warn "No NVIDIA GPU detected via nvidia-smi."
    read -rp "Continue anyway? (y/N): " confirm
    if [[ ! "${confirm:-N}" =~ ^[Yy]$ ]]; then
      info "GPU launch cancelled."
      return
    fi
  fi

  compose_up "Core + AI + GPU Stack" "${BASE_ARGS[@]}" "${AI_ARGS[@]}" "${GPU_ARGS[@]}"
}

main_menu() {
  while true; do
    print_header
    echo "1) Run Core Stack"
    echo "2) Run Core + AI"
    echo "3) Run Core + AI + GPU"
    echo "4) Show Status"
    echo "5) Show Logs"
    echo "6) Rebuild Specific Service"
    echo "7) Stop Stack"
    echo "8) Reset Stack (delete volumes)"
    echo "9) Exit"
    echo
    read -rp "Select option: " choice
    echo

    case "$choice" in
      1) run_core; pause ;;
      2) run_core_ai; pause ;;
      3) run_core_ai_gpu; pause ;;
      4) show_status; pause ;;
      5) show_logs ;;
      6) rebuild_service; pause ;;
      7) compose_down_all; pause ;;
      8) compose_reset_all; pause ;;
      9) exit 0 ;;
      *) warn "Invalid option."; pause ;;
    esac
  done
}

check_docker
main_menu
