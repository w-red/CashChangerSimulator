import xml.etree.ElementTree as ET
import sys
from collections import defaultdict

def analyze_coverage(xml_file):
    try:
        tree = ET.parse(xml_file)
        root = tree.getroot()
    except Exception as e:
        print(f"Error parsing XML: {e}")
        return

    modules = root.findall('.//module')
    
    all_class_stats = []

    for module in modules:
        module_name = module.get('name')
        functions = module.findall('.//function')
        
        class_lines = defaultdict(lambda: {'covered': 0, 'total': 0})
        
        for func in functions:
            class_name = func.get('type_name')
            namespace = func.get('namespace')
            if not class_name or not namespace:
                continue
            full_class_name = f"{namespace}.{class_name}"
            
            lines_covered = int(func.get('lines_covered', 0))
            lines_not_covered = int(func.get('lines_not_covered', 0))
            lines_partially_covered = int(func.get('lines_partially_covered', 0))
            
            total_lines = lines_covered + lines_not_covered + lines_partially_covered
            
            if total_lines == 0:
                continue
                
            class_lines[full_class_name]['covered'] += lines_covered
            class_lines[full_class_name]['total'] += total_lines
            class_lines[full_class_name]['module'] = module_name

        for name, stats in class_lines.items():
            coverage = (stats['covered'] / stats['total']) * 100
            all_class_stats.append({
                'name': name,
                'module': stats['module'],
                'coverage': coverage,
                'total_lines': stats['total'],
                'covered_lines': stats['covered']
            })

    # Filter out small classes (e.g., < 10 lines) and sort by coverage (ascending)
    low_coverage_classes = [c for c in all_class_stats if c['total_lines'] >= 20]
    low_coverage_classes.sort(key=lambda x: x['coverage'])

    print(f"{'Module':<40} | {'Class':<60} | {'Coverage':<10} | {'Lines'}")
    print("-" * 125)
    for c in low_coverage_classes[:40]:
        print(f"{c['module']:<40} | {c['name']:<60} | {c['coverage']:>8.2f}% | {c['total_lines']}")

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python analyze_coverage.py <coverage_report.xml>")
    else:
        analyze_coverage(sys.argv[1])
