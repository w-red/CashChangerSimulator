import pandas as pd
df = pd.read_excel(r'C:\Users\ITI202301003_User\Documents\ObsidianVault\Vault\Researched\CashChangerSimulator.All.xlsx')
print(df.columns.tolist())

# Try to find columns that match Cyclomatic Complexity and Maintainability Index
cyc_col = [c for c in df.columns if 'サイクロマティック' in c or 'Cyclomatic' in c][0]
mi_col = [c for c in df.columns if '保守容易性' in c or 'Maintainability' in c][0]

df_clean = df.dropna(subset=[cyc_col])
print('\n--- 高複雑度 (Top 5) ---')
print(df_clean.sort_values(by=cyc_col, ascending=False).head(5)[['プロジェクト', '型', cyc_col, mi_col]].to_string())

print('\n--- 保守容易性インデックス低 (Bottom 5) ---')
print(df_clean.sort_values(by=mi_col, ascending=True).head(5)[['プロジェクト', '型', cyc_col, mi_col]].to_string())
