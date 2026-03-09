import os
import re
import sys

# 問題のあるパターンとその説明のリスト
# (Regex Pattern, Error Message)
ERROR_PATTERNS = [
    (r'ListBox\s+[^>]*SelectionMode="None"', 'ListBox does not support SelectionMode="None". Use ItemsControl or SelectionMode="Single" instead.'),
    (r'Foreground="Transparent"', 'Foreground="Transparent" might make text invisible. Please verify if this is intentional.'),
]

def check_xaml_file(file_path):
    errors = []
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
        for pattern, message in ERROR_PATTERNS:
            if re.search(pattern, content, re.IGNORECASE):
                errors.append(f"  [ERROR] {message}")
    return errors

def main():
    root_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    src_dir = os.path.join(root_dir, 'src')
    
    found_errors = False
    print(f"Scanning XAML files in {src_dir}...")
    
    for root, _, files in os.walk(src_dir):
        for file in files:
            if file.endswith('.xaml'):
                file_path = os.path.join(root, file)
                errors = check_xaml_file(file_path)
                if errors:
                    print(f"\n{os.path.relpath(file_path, root_dir)}:")
                    for error in errors:
                        print(error)
                    found_errors = True

    if found_errors:
        print("\n[FAILED] Known XAML issues found. Please fix them to avoid runtime exceptions.")
        sys.exit(1)
    else:
        print("\n[SUCCESS] No known XAML issues found.")
        sys.exit(0)

if __name__ == "__main__":
    main()
