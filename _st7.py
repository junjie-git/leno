import subprocess

out = []


def run(cmd, timeout=90):
    r = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout, cwd="D:/work/learn/Leno")
    return (r.stdout + r.stderr).strip()


out.append("=== 容器")
out.append(run(["docker", "ps", "-a", "--format", "{{.Names}} | {{.Status}}"]) or "(无)")
out.append("=== 镜像")
out.append(run(["docker", "images", "--format", "{{.Repository}}:{{.Tag}}"]) or "(无)")

with open("_st7.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("OK")
