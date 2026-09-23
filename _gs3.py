import subprocess

out = []
r = subprocess.run(["git", "status", "--porcelain=v1", "-uall"], capture_output=True, text=True,
                   cwd="D:/work/learn/Leno", timeout=120)
lines = r.stdout.splitlines()
out.append("总变更: " + str(len(lines)))
out.append("--- 未跟踪（??）")
untracked = [l for l in lines if l.startswith("??")]
out.extend(untracked[:40])
out.append("未跟踪数: " + str(len(untracked)))

with open("_gs3.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("OK")
