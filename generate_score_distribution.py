import json
import matplotlib.pyplot as plt
import numpy as np

plt.rcParams['font.sans-serif'] = ['SimHei', 'DejaVu Sans']
plt.rcParams['axes.unicode_minus'] = False

file_path = 'initial_users.json'

with open(file_path, 'r', encoding='utf-8') as f:
    data = json.load(f)

scores = [user['Score'] for user in data]

scores = np.array(scores)
print(f"总用户数: {len(scores)}")
print(f"最低分数: {scores.min()}")
print(f"最高分数: {scores.max()}")
print(f"平均分数: {scores.mean():.2f}")

percentiles = [25, 50, 75, 90, 95, 99]
for p in percentiles:
    print(f"{p}% 分位数: {np.percentile(scores, p):.0f}")

n_bins = 30
bin_edges = np.linspace(scores.min(), scores.max(), n_bins + 1)
bin_counts, bins = np.histogram(scores, bins=bin_edges)

bin_centers = (bin_edges[:-1] + bin_edges[1:]) / 2
bin_width = bin_edges[1] - bin_edges[0]

fig, ax = plt.subplots(figsize=(14, 10))

colors = plt.cm.viridis(np.linspace(0.2, 0.8, len(bin_centers)))

bars = ax.barh(bin_centers, bin_counts, height=bin_width * 0.9, color=colors, edgecolor='white', linewidth=0.5)

ax.set_xlabel('人数', fontsize=14, fontweight='bold')
ax.set_ylabel('分数', fontsize=14, fontweight='bold')
ax.set_title('用户分数分布直方图\n(横轴人数，纵轴分数，条形越胖人数越多)', fontsize=16, fontweight='bold')

ax.yaxis.set_major_formatter(plt.FuncFormatter(lambda x, p: format(int(x), ',')))

for i, (count, center) in enumerate(zip(bin_counts, bin_centers)):
    if count > 0:
        ax.text(count + max(bin_counts) * 0.01, center, f'{count:,}', 
                va='center', ha='left', fontsize=8, color=colors[i])

max_count_idx = np.argmax(bin_counts)
bars[max_count_idx].set_edgecolor('red')
bars[max_count_idx].set_linewidth(2)

ax.grid(axis='x', alpha=0.3, linestyle='--')
ax.set_axisbelow(True)

plt.tight_layout()
plt.savefig('score_distribution.png', dpi=150, bbox_inches='tight', facecolor='white')
plt.close()

print("\n图表已保存为 score_distribution.png")
