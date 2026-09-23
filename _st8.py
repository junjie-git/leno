import subprocess
import time

out = []
r = subprocess.run(["docker", "compose", "up", "-d", "inventory-api"],
                   capture_output=True, text=True, timeout=300, cwd="D:/work/learn/Leno")
out.append("up rc=" + str(r.returncode) + " :: " + (r.stdout + r.stderr)[-250:])

# 等健康（最多 90s：容器启动要跑迁移 + Consul 拉配置）
deadline = time.time() + 90
status = "unknown"
while time.time() < deadline:
    r2 = subprocess.run(["docker", "ps", "--filter", "name=leno-inventory-api", "--format", "{{.Status}}"],
                        capture_output=True, text=True, timeout=30)
    status = r2.stdout.strip()
    if "healthy" in status:
        break
    time.sleep(5)
out.append("inventory-api status: " + (status or "(不存在——可能崩溃，查日志)"))

if "healthy" not in status:
    r3 = subprocess.run(["docker", "logs", "--tail", "25", "leno-inventory-api"],
                        capture_output=True, text=True, timeout=60)
    out.append("=== 日志尾部")
    out.append(r3.stdout[-1200:] + r3.stderr[-400:])

with open("_st8.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("OK")
