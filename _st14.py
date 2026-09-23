import subprocess
import time

out = []
r = subprocess.run(["docker", "compose", "config", "--quiet"], capture_output=True, text=True,
                   timeout=120, cwd="D:/work/learn/Leno")
out.append(f"compose config rc={r.returncode}")

r = subprocess.run(["docker", "compose", "up", "-d", "--force-recreate", "inventory-api"],
                   capture_output=True, text=True, timeout=300, cwd="D:/work/learn/Leno")
out.append(f"recreate rc={r.returncode} :: {(r.stdout + r.stderr)[-150:]}")

time.sleep(35)
r2 = subprocess.run(["docker", "ps", "--filter", "name=leno-inventory-api", "--format", "{{.Status}}"],
                    capture_output=True, text=True, timeout=30)
out.append("status: " + r2.stdout.strip())

with open("_st14.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("DONE")
