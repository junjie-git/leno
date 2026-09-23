import subprocess

out = []


def sh(cmd, timeout=900):
    r = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout, cwd="D:/work/learn/Leno")
    return r.returncode, (r.stdout + r.stderr)


# 1) add 全部
rc, t = sh(["git", "add", "-A"])
out.append(f"add rc={rc}")

# 2) commit（-F 消息文件，规避中文引号转义问题）
rc, t = sh(["git", "commit", "-F", ".commit-msg.txt"], timeout=600)
out.append(f"commit rc={rc} :: {t[-500:]}")

# 3) 确认工作树干净 + 提交哈希
rc, t = sh(["git", "status", "--porcelain=v1"])
out.append("剩余变更: " + str(len(t.splitlines())))
rc, t = sh(["git", "log", "--oneline", "-1"])
out.append("HEAD: " + t.strip())

with open("_git2.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("DONE")
