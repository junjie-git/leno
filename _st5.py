import subprocess

out = []
r = subprocess.run(["docker", "compose", "up", "-d", "consul", "redis", "rabbitmq"],
                   capture_output=True, text=True, cwd="D:/work/learn/Leno", timeout=300)
out.append("up rc=" + str(r.returncode))
out.append((r.stdout + r.stderr)[-400:])

r2 = subprocess.run(["docker", "ps", "--format", "{{.Names}} | {{.Status}}"],
                    capture_output=True, text=True, timeout=60)
out.append("=== ps")
out.append(r2.stdout or "(空)")

try:
    tail = open("_pl_sql.txt", encoding="utf-8", errors="replace").read().splitlines()[-4:]
    out.append("=== mssql 拉取尾部")
    out.extend(tail)
except Exception as ex:
    out.append("ERR " + str(ex)[:60])

with open("_st5.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("OK")
