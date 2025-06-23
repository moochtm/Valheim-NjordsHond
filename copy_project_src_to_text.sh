#!/bin/bash

# Directory to scan
PROJECT_DIR="$HOME/Code/Valheim/Heimssaga/src"

# Output file
OUTPUT_FILE="./project_structure.txt"

# Clear previous output
> "$OUTPUT_FILE"

# Recursively list directory structure and file contents
find "$PROJECT_DIR" -type f -name "*.cs" | while read -r file; do
    echo "===== FILE: ${file#$PROJECT_DIR/} =====" >> "$OUTPUT_FILE"
    cat "$file" >> "$OUTPUT_FILE"
    echo -e "\n" >> "$OUTPUT_FILE"
done

# Copy to clipboard
cat "$OUTPUT_FILE" | pbcopy

echo "Project structure and contents copied to clipboard."