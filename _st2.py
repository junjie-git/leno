import subprocess

out = []
r = subprocess.run(["docker", "compose", "config", "--quiet"], capture_output=True, text=True, cwd="D:/work/learn/Leno", timeout=120)
out.append(f"compose config rc={r.returncode} :: {r.stderr[:150]}")

for name in ["_pl_small_done.txt", "_pl_sql_done.txt", "_build_done.txt"]:
    p = "D:/work/learn/Leno/" + name
    try:
        out.append(name + " :: " + open(p, encoding="utf-8").read().strip())
    except FileNotFoundError:
        out.append(name + " :: 未完成")

r2 = subprocess.run(["docker", "images", "--format", "{{.Repository}}:{{.Tag}}"],
                    capture_output=True, text=True, timeout=60)
out.append("images: " + (r2.stdout.strip().replace("\n", ", ") or "(空)"))

with open("_st2.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("OK")
