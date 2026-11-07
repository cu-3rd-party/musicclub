#!/usr/bin/env bash
# GitOps-деплой: ставит тег образов musicclub-<env>-{backend,frontend} в клоне pxc1984/homelab и пушит.
# Argo CD выкатывает изменение сам. Использование: deploy-images.sh <клон homelab> <dev|prod> <полный SHA>
set -euo pipefail

homelab="$1"
environment="$2"
tag="$3"

case "$environment" in
  dev | prod) ;;
  *) echo "::error::Окружение должно быть dev или prod: $environment"; exit 1 ;;
esac
if ! [[ "$tag" =~ ^[0-9a-f]{40}$ ]]; then
  echo "::error::Нужен полный SHA из 40 символов: $tag"
  exit 1
fi

cd "$homelab"
stack="musicclub-${environment}"
for service in backend frontend; do
  values="apps/${stack}-${service}/values.yaml"
  if [ ! -f "$values" ]; then
    echo "::error::$values не найден в homelab"
    exit 1
  fi
  sed -i -E \
    -e "s|^(  repository: ).*|\1git.cu3rd.ru/cu3rd/musicclub-${service}|" \
    -e "s/^(  tag: )[^ ]+/\1${tag}/" "$values"
done

if git diff --quiet; then
  echo "${stack} уже на ${tag::7}"
  exit 0
fi
git --no-pager diff
git config user.name "forgejo-actions[bot]"
git config user.email "cu3rd-ci@noreply.git.cu3rd.ru"
git commit -qam "${stack}: deploy ${tag::7}"
# параллельный деплой другого приложения мог успеть запушить раньше
for attempt in 1 2 3; do
  git push -q && exit 0
  git pull -q --rebase
done
exit 1
