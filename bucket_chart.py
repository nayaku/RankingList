import matplotlib.pyplot as plt
import matplotlib as mpl
import re

# 设置中文字体
mpl.rcParams['font.sans-serif'] = ['SimHei']  # 指定默认字体为黑体
mpl.rcParams['axes.unicode_minus'] = False  # 解决保存图像时负号'-'显示为方块的问题

# 数据行
lines = [
    "Bucket with 6 users: 1 buckets",
    "Bucket with 7 users: 5 buckets",
    "Bucket with 8 users: 8 buckets",
    "Bucket with 9 users: 8 buckets",
    "Bucket with 10 users: 12 buckets",
    "Bucket with 11 users: 15 buckets",
    "Bucket with 12 users: 16 buckets",
    "Bucket with 13 users: 32 buckets",
    "Bucket with 14 users: 28 buckets",
    "Bucket with 15 users: 48 buckets",
    "Bucket with 16 users: 55 buckets",
    "Bucket with 17 users: 68 buckets",
    "Bucket with 18 users: 66 buckets",
    "Bucket with 19 users: 85 buckets",
    "Bucket with 20 users: 76 buckets",
    "Bucket with 21 users: 73 buckets",
    "Bucket with 22 users: 72 buckets",
    "Bucket with 23 users: 64 buckets",
    "Bucket with 24 users: 59 buckets",
    "Bucket with 25 users: 62 buckets",
    "Bucket with 26 users: 53 buckets",
    "Bucket with 27 users: 50 buckets",
    "Bucket with 28 users: 53 buckets",
    "Bucket with 29 users: 48 buckets",
    "Bucket with 30 users: 41 buckets",
    "Bucket with 31 users: 36 buckets",
    "Bucket with 32 users: 43 buckets",
    "Bucket with 33 users: 41 buckets",
    "Bucket with 34 users: 42 buckets",
    "Bucket with 35 users: 36 buckets",
    "Bucket with 36 users: 41 buckets",
    "Bucket with 37 users: 42 buckets",
    "Bucket with 38 users: 42 buckets",
    "Bucket with 39 users: 44 buckets",
    "Bucket with 40 users: 41 buckets",
    "Bucket with 41 users: 42 buckets",
    "Bucket with 42 users: 34 buckets",
    "Bucket with 43 users: 34 buckets",
    "Bucket with 44 users: 49 buckets",
    "Bucket with 45 users: 45 buckets",
    "Bucket with 46 users: 38 buckets",
    "Bucket with 47 users: 37 buckets",
    "Bucket with 48 users: 34 buckets",
    "Bucket with 49 users: 41 buckets",
    "Bucket with 50 users: 24 buckets",
    "Bucket with 51 users: 34 buckets",
    "Bucket with 52 users: 39 buckets",
    "Bucket with 53 users: 40 buckets",
    "Bucket with 54 users: 34 buckets",
    "Bucket with 55 users: 29 buckets",
    "Bucket with 56 users: 35 buckets",
    "Bucket with 57 users: 32 buckets",
    "Bucket with 58 users: 36 buckets",
    "Bucket with 59 users: 41 buckets",
    "Bucket with 60 users: 47 buckets",
    "Bucket with 61 users: 50 buckets",
    "Bucket with 62 users: 52 buckets",
    "Bucket with 63 users: 49 buckets",
    "Bucket with 64 users: 62 buckets",
    "Bucket with 65 users: 17 buckets",
    "Bucket with 66 users: 21 buckets",
    "Bucket with 67 users: 17 buckets",
    "Bucket with 68 users: 13 buckets",
    "Bucket with 69 users: 10 buckets",
    "Bucket with 70 users: 3 buckets",
    "Bucket with 71 users: 7 buckets",
    "Bucket with 72 users: 6 buckets",
    "Bucket with 73 users: 2 buckets",
    "Bucket with 74 users: 2 buckets",
    "Bucket with 75 users: 1 buckets",
    "Bucket with 76 users: 7 buckets",
    "Bucket with 78 users: 2 buckets",
    "Bucket with 79 users: 7 buckets",
    "Bucket with 80 users: 1 buckets",
    "Bucket with 81 users: 5 buckets",
    "Bucket with 82 users: 3 buckets",
    "Bucket with 83 users: 5 buckets",
    "Bucket with 84 users: 2 buckets",
    "Bucket with 85 users: 3 buckets",
    "Bucket with 87 users: 5 buckets",
    "Bucket with 88 users: 2 buckets",
    "Bucket with 89 users: 6 buckets",
    "Bucket with 90 users: 2 buckets",
    "Bucket with 91 users: 3 buckets",
    "Bucket with 92 users: 3 buckets",
    "Bucket with 94 users: 6 buckets",
    "Bucket with 95 users: 4 buckets",
    "Bucket with 96 users: 3 buckets",
    "Bucket with 97 users: 4 buckets",
    "Bucket with 98 users: 1 buckets",
    "Bucket with 99 users: 4 buckets",
    "Bucket with 100 users: 1 buckets",
    "Bucket with 101 users: 2 buckets",
    "Bucket with 102 users: 6 buckets",
    "Bucket with 103 users: 1 buckets",
    "Bucket with 104 users: 5 buckets",
    "Bucket with 105 users: 2 buckets",
    "Bucket with 106 users: 5 buckets",
    "Bucket with 107 users: 4 buckets",
    "Bucket with 108 users: 1 buckets",
    "Bucket with 109 users: 2 buckets",
    "Bucket with 110 users: 1 buckets",
    "Bucket with 111 users: 3 buckets",
    "Bucket with 112 users: 4 buckets",
    "Bucket with 114 users: 4 buckets",
    "Bucket with 115 users: 2 buckets",
    "Bucket with 116 users: 3 buckets",
    "Bucket with 117 users: 2 buckets",
    "Bucket with 118 users: 3 buckets",
    "Bucket with 119 users: 2 buckets",
    "Bucket with 120 users: 3 buckets",
    "Bucket with 121 users: 2 buckets",
    "Bucket with 122 users: 3 buckets",
    "Bucket with 123 users: 2 buckets",
    "Bucket with 124 users: 1 buckets",
    "Bucket with 125 users: 4 buckets",
    "Bucket with 126 users: 3 buckets",
    "Bucket with 127 users: 1 buckets",
    "Bucket with 128 users: 1 buckets"
]

# 解析数据
x = []
y = []

pattern = r"Bucket with (\d+) users: (\d+) buckets"

for line in lines:
    match = re.match(pattern, line)
    if match:
        users = int(match.group(1))
        buckets = int(match.group(2))
        x.append(users)
        y.append(buckets)

# 创建折线图
plt.figure(figsize=(12, 6))
plt.plot(x, y, marker='o', linestyle='-', color='b')
plt.fill_between(x, y, alpha=0.3, color='b')  # 添加填充区域
plt.title('桶分布')
plt.xlabel('桶内用户数')
plt.ylabel('桶数量')
plt.grid(True)
plt.tight_layout()

# 保存图表
plt.savefig('bucket_chart.png', dpi=300)
plt.close()

print("图表已生成并保存为 bucket_chart.png")