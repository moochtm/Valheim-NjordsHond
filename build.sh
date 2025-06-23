#!/bin/bash

# Paths
VALHEIM_PATH="$HOME/Library/Application Support/Steam/steamapps/common/Valheim"
PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$PROJECT_DIR/build"
CONFIG_DIR="$VALHEIM_PATH/BepInEx/config"
VERSION_FILE="$PROJECT_DIR/VERSION.txt"
LOG_FILE="$PROJECT_DIR/build.log"

PLUGIN_NAME="Valheim.NjordsHond"
PLUGIN_SOURCE_FILE="$PROJECT_DIR/src/${PLUGIN_NAME}.cs"
PROJECT_FILE="$PROJECT_DIR/${PLUGIN_NAME}.csproj"
CONFIG_FILE="$PROJECT_DIR/BepInEx/config/com.scoobymooch.${PLUGIN_NAME}.cfg"

# Read version from VERSION.txt
if [ ! -f "$VERSION_FILE" ]; then
    echo "VERSION.txt not found. Please create this file."
    exit 1
fi
VERSION=$(cat "$VERSION_FILE" | tr -d '\n' | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//')
echo "[INFO] Using version $VERSION from VERSION.txt" | tee -a "$LOG_FILE"

echo ""
echo "[INFO] Bumping version..."

if [ "$1" == "nobump" ]; then
    echo "[INFO] Skipping version bump. Using existing version."
    VERSION=$(cat "$VERSION_FILE" | tr -d '\n' | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//')
else
    PART="${1:-patch}"

    # Read current version
    IFS='.' read -r MAJOR MINOR PATCH <<< "$VERSION"

    # Bump the appropriate part
    case "$PART" in
        major)
            MAJOR=$((MAJOR + 1))
            MINOR=0
            PATCH=0
            ;;
        minor)
            MINOR=$((MINOR + 1))
            PATCH=0
            ;;
        patch)
            PATCH=$((PATCH + 1))
            ;;
        *)
            echo "Invalid version part: $PART"
            exit 1
            ;;
    esac

    # Write the new version
    NEW_VERSION="$MAJOR.$MINOR.$PATCH"
    echo "$NEW_VERSION" > "$VERSION_FILE"
    echo "[INFO] Version bumped to $NEW_VERSION"

    if [ -f "$PLUGIN_SOURCE_FILE" ]; then
        echo "[INFO] Updating version in $PLUGIN_SOURCE_FILE"
        sed -i '' -E "s/(private const string VERSION = \")[0-9]+\.[0-9]+\.[0-9]+\"/\1$NEW_VERSION\"/" "$PLUGIN_SOURCE_FILE"
    else
        echo "[WARNING] Could not find $PLUGIN_SOURCE_FILE to update version"
    fi

    VERSION="$NEW_VERSION"
fi

echo "[INFO] Building v$VERSION..." | tee -a "$LOG_FILE"

# Clean up old build directory
rm -rf "$BUILD_DIR"/* 2>> "$LOG_FILE"

# Build the project
# dotnet build "$PROJECT_FILE" -c Release -p:Version=$VERSION -o "$BUILD_DIR" 2>> "$LOG_FILE"
dotnet build "$PROJECT_FILE" /v:diag -c Release -o "$BUILD_DIR" 2>> "$LOG_FILE"

# Create the new versioned directory
mkdir -p "$PLUGIN_DIR"



# Copy the DLL to the BepInEx/scripts directory
SCRIPTS_DIR="$VALHEIM_PATH/BepInEx/scripts"
mkdir -p "$SCRIPTS_DIR"
echo "[INFO] Copying DLL and PDB to $SCRIPTS_DIR" | tee -a "$LOG_FILE"
cp "$BUILD_DIR"/*.dll "$SCRIPTS_DIR/" 2>> "$LOG_FILE"
cp "$BUILD_DIR"/*.pdb "$SCRIPTS_DIR/" 2>> "$LOG_FILE"

# Copy the config file
echo "[INFO] Copying config to $CONFIG_DIR" | tee -a "$LOG_FILE"
cp "$CONFIG_FILE" "$CONFIG_DIR/" 2>> "$LOG_FILE"

# Package for release
RELEASE_FOLDER="$BUILD_DIR/MattBarr-NjordsHond-$VERSION"
RELEASE_ZIP="$BUILD_DIR/MattBarr-NjordsHond-$VERSION.zip"

mkdir -p "$RELEASE_FOLDER"
cp "$BUILD_DIR"/*.dll "$RELEASE_FOLDER/"
[ -f "$PROJECT_DIR/manifest.json" ] && cp "$PROJECT_DIR/manifest.json" "$RELEASE_FOLDER/"
[ -f "$PROJECT_DIR/README.md" ] && cp "$PROJECT_DIR/README.md" "$RELEASE_FOLDER/"
[ -f "$PROJECT_DIR/CHANGELOG.md" ] && cp "$PROJECT_DIR/CHANGELOG.md" "$RELEASE_FOLDER/"
[ -f "$PROJECT_DIR/icon.png" ] && cp "$PROJECT_DIR/icon.png" "$RELEASE_FOLDER/"

echo "[INFO] Creating ZIP package $RELEASE_ZIP" | tee -a "$LOG_FILE"
cd "$BUILD_DIR" && zip -r "MattBarr-NjordsHond-$VERSION.zip" "MattBarr-NjordsHond-$VERSION" > /dev/null && cd -

echo "[SUCCESS] Build complete!" | tee -a "$LOG_FILE"
echo "[INFO] Plugin DLL copied to $SCRIPTS_DIR" | tee -a "$LOG_FILE"
echo "[INFO] Config copied to $CONFIG_DIR" | tee -a "$LOG_FILE"
echo "[INFO] Log file: $LOG_FILE"