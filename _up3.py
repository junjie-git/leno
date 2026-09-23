import subprocess

r = subprocess.run(["docker", "ps", "--format", "{{.Names}} | {{.Status}}"],
                   capture_output=True, text=True, timeout=60)
with open("_up2.txt", "w", encoding="utf-8") as f:
    f.write(r.stdout)
print("rc:", r.returncode)
