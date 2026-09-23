import subprocess

out = []
r = subprocess.run(["docker", "inspect", "leno-inventory-api", "--format", "{{range .Config.Env}}{{println .}}{{end}}"],
                   capture_output=True, text=True, timeout=30)
for line in r.stdout.splitlines():
    if any(k in line for k in ("Kestrel", "ASPNETCORE", "URLS", "Consul")):
        out.append(line)

# 容器内实际监听端口（/proc/net/tcp，十六进制）
r2 = subprocess.run(["docker", "exec", "leno-inventory-api", "cat", "/proc/net/tcp"],
                    capture_output=True, text=True, timeout=30)
listeners = set()
for line in r2.stdout.splitlines()[1:]:
    parts = line.split()
    if len(parts) > 3 and parts[3] == "0A":  # LISTEN
        port_hex = parts[1].split(":")[1]
        listeners.add(int(port_hex, 16))
out.append("容器内监听端口: " + str(sorted(listeners)))

with open("_st15.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("OK")
