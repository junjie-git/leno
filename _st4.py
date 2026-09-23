import subprocess

out = []


def tail(p, n=8):
    try:
        return "\n".join(open(p, encoding="utf-8", errors="replace").read().splitlines()[-n:])
    except Exception as ex:
        return "ERR " + str(ex)[:80]


out.append("=== _up.txt 尾部")
out.append(tail("_up.txt"))
out.append("=== _pl_sql.txt 尾部")
out.append(tail("_pl_sql.txt"))

# 重试：前台启动 consul/redis/rabbitmq（镜像已就绪）
r = subprocess.run(["docker", "compose", "up", "-d", "consul", "redis", "rabbitmq"],
                   capture_output=True, text=True, cwd="D:/work/learn/Leno", timeout=300)
out.append("=== up rc=" + str(r.returncode))
out.append((r.stdout + r.stderr)[-600:])

r2 = subprocess.run(["docker", "ps", "--format", "{{.Names}} | {{.Status}}"],
                    capture_output=True, text=True, timeout=60)
out.append("=== ps")
out.append(r2.stdout)

with open("_st4.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("OK")
