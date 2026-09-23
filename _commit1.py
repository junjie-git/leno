import glob
import os
import subprocess

out = []


def sh(cmd, timeout=600):
    r = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout, cwd="D:/work/learn/Leno")
    return r.returncode, (r.stdout + r.stderr)


# 1) 清理全部 _* 调试临时文件（根目录）
removed = 0
for pat in ["_*.py", "_*.txt", "_*.md"]:
    for p in glob.glob(pat):
        os.remove(p)
        removed += 1
out.append(f"清理临时文件 {removed} 个")

# 2) .gitignore 补 .env / TestResults
gi_lines = []
if os.path.exists(".gitignore"):
    gi_lines = open(".gitignore", encoding="utf-8").read().splitlines()
changed_gi = False
for k in [".env", "TestResults/"]:
    if k not in gi_lines:
        gi_lines.append(k)
        changed_gi = True
if changed_gi:
    open(".gitignore", "a", encoding="utf-8").write("\n# 本地 Docker 环境变量与测试结果\n.env\nTestResults/\n")
out.append(".gitignore 追加: " + str(changed_gi))

# 3) add + 状态
rc, t = sh(["git", "add", "-A"], timeout=300)
out.append(f"git add rc={rc}")
rc, t = sh(["git", "status", "--porcelain=v1"], timeout=300)
out.append("staged 变更: " + str(len(t.splitlines())))

with open("_git1.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("DONE")
