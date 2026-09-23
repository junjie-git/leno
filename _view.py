import os

out = []
for p in ["_seed3_out.txt", "_seed3_err.txt"]:
    out.append("=== " + p + " exists=" + str(os.path.exists(p)))
    if os.path.exists(p):
        out.extend(open(p, encoding="utf-8").read().splitlines()[-8:])
with open("_seed3_view.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("OK")
