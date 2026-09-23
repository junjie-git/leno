import subprocess

out = []


def run(cmd, timeout=90):
    r = subprocess.run(cmd, capture_output=True, timeout=timeout)
    return r.stdout.decode("utf-8", errors="replace") + (r.stderr.decode("utf-8", errors="replace")[:300])


out.append("=== docker ps")
out.append(run(["docker", "ps", "-a", "--format", "{{.Names}} | {{.Status}}"]))
out.append("=== build 尾部")
try:
    out.append("\n".join(open("_build.txt", encoding="utf-8", errors="replace").read().splitlines()[-6:]))
except Exception as ex:
    out.append("ERR " + str(ex)[:80])

with open("_st3.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("OK")
