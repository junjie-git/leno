out = []
r = os.popen("docker ps --format {{.Names}}|{{.Status}}").read()
out.append(r.strip())
with open("_up2.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("OK")
