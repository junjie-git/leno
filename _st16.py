import subprocess

out = []
r = subprocess.run(["docker", "ps", "-a", "--filter", "name=leno-inventory-api", "--format", "{{.Status}}"],
                   capture_output=True, text=True, timeout=30)
out.append("status: " + r.stdout.strip())
r = subprocess.run(["docker", "logs", "--tail", "40", "leno-inventory-api"],
                   capture_output=True, text=True, timeout=60)
text = (r.stdout + r.stderr)
# 只保留关键行
key = [l for l in text.splitlines() if any(k in l for k in
       ("Now listening", "Overriding", "Kestrel", "敏感配置", "Exception", "WARN", "Listening", "migration", "Migrat", "error", "fail", "Fail"))]
out.extend(key[-25:])

with open("_st16.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("OK")
