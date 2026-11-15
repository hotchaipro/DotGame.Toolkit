#!/bin/bash
# test-packages.sh [platform...]: the toolkit's samples built from the packages alone, by the framework's
# packaging/test-packages.sh, in the clone DotGameFrameworkDir names (required), with the toolkit's
# own checks (packaging/test-packages-checks.sh).
#
#   VERSION=0.1.0-local.1 packaging/test-packages.sh desktop webgl android ios
#
# The packages come from FEEDS, folders of .nupkg: by default this repository's artifacts/packages
# and the framework's, which between them hold the toolkit's and the framework's (packaging/pack.sh in
# each) and the build machine's tool package (the framework's packaging/pack-tools.sh).
set -uo pipefail
REPO=$(cd "$(dirname "$0")/.." && pwd)
FRAMEWORK=${DotGameFrameworkDir:-}
[ -f "$FRAMEWORK/packaging/test-packages.sh" ] || { echo "test-packages.sh: no framework at $FRAMEWORK (DotGameFrameworkDir)"; exit 1; }
FRAMEWORK=$(cd "$FRAMEWORK" && pwd)
export FEEDS=${FEEDS:-$REPO/artifacts/packages $FRAMEWORK/artifacts/packages}
export DOTGAME_LIBRARY_DIR=$REPO DOTGAME_LIBRARY_PACKAGE=DotGame.Toolkit DOTGAME_LIBRARY_CHECKS=$REPO/packaging/test-packages-checks.sh
exec "$FRAMEWORK/packaging/test-packages.sh" "$@"
