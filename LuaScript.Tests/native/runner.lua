local shimPath = arg[1]
local vectorsPath = arg[2]

assert(loadfile(shimPath))()

local f = assert(io.open(vectorsPath, "r"))
local out = {}
for line in f:lines() do
    local chunk = loadstring("return " .. line)
    if not chunk then
        out[#out + 1] = "ERROR"
    else
        local ok, res = pcall(chunk)
        if not ok then
            out[#out + 1] = "ERROR"
        elseif type(res) == "boolean" then
            out[#out + 1] = res and "true" or "false"
        else
            out[#out + 1] = string.format("%.17g", res)
        end
    end
end
f:close()

io.write(table.concat(out, "\n"))
