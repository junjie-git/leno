import subprocess

out = []


def run(cmd, timeout=600):
    r = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout, cwd="D:/work/learn/Leno")
    return (r.stdout + r.stderr)[-500:], r.returncode


# 1) mssql 拉取状态
out.append("=== mssql 拉取尾部")
out.append("".join(open("_pl_sql.txt", encoding="utf-8", errors="replace").read().splitlines()[-3:]))

# 2) 启动 sqlserver（镜像就绪才成功）
t, rc = run(["docker", "compose", "up", "-d", "sqlserver"])
out.append(f"up sqlserver rc={rc} :: {t[-200:]}")

# 3) 启动 inventory-api
t, rc = run(["docker", "compose", "up", "-d", "inventory-api"])
out.append(f"up inventory-api rc={rc} :: {t[-200:]}")

# 4) 容器状态
t, _ = run(["docker", "ps", "--format", "{{.Names}} | {{.Status}}"])
out.append("=== ps")
out.append(t)

with open("_st6.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("OK")
