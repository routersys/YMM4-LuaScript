local ffi = require("ffi")

ffi.cdef [[
typedef void* HANDLE;
HANDLE OpenFileMappingA(unsigned long, int, const char*);
void* MapViewOfFile(HANDLE, unsigned long, unsigned long, unsigned long, size_t);
int UnmapViewOfFile(const void*);
int CloseHandle(HANDLE);
HANDLE OpenEventA(unsigned long, int, const char*);
unsigned long WaitForSingleObject(HANDLE, unsigned long);
int SetEvent(HANDLE);
unsigned long GetLastError();
]]

local k32 = ffi.load("kernel32")
local FILE_MAP_ALL_ACCESS = 0x000F001F
local EVENT_ALL_ACCESS = 0x1F0003
local INFINITE = 0xFFFFFFFF

local mapName = arg[1]
local mapSize = tonumber(arg[2])
local workEventName = arg[3]
local doneEventName = arg[4]
local shimPath = arg[5]

local hMap = k32.OpenFileMappingA(FILE_MAP_ALL_ACCESS, 0, mapName)
assert(hMap ~= nil, "OpenFileMapping failed")
local view = k32.MapViewOfFile(hMap, FILE_MAP_ALL_ACCESS, 0, 0, mapSize)
assert(view ~= nil, "MapViewOfFile failed")

local workEvent = k32.OpenEventA(EVENT_ALL_ACCESS, 0, workEventName)
local doneEvent = k32.OpenEventA(EVENT_ALL_ACCESS, 0, doneEventName)
assert(workEvent ~= nil and doneEvent ~= nil, "OpenEvent failed")

local base = ffi.cast("uint8_t*", view)
local i32 = ffi.cast("int32_t*", base)
local f64 = ffi.cast("double*", base + 64)

local OFF_COMMAND = 1
local OFF_STATUS = 2
local OFF_WIDTH = 3
local OFF_HEIGHT = 4
local OFF_SCRIPTLEN = 5
local OFF_PIXELSDIRTY = 6
local OFF_ERRORLEN = 7
local OFF_CB_FRAME = 8
local OFF_CB_TAGLEN = 9
local OFF_CB_FOUND = 10
local OFF_CB_KIND = 11
local OFF_LOAD_RESULT_W = 12
local OFF_LOAD_RESULT_H = 13
local SCRIPT_OFFSET = 64 + 64 * 8
local ERROR_OFFSET = SCRIPT_OFFSET + 128 * 1024
local CB_TAG_OFFSET = ERROR_OFFSET + 4 * 1024
local CB_TAG_MAX = 4096
local CB_RESULT_OFFSET = CB_TAG_OFFSET + CB_TAG_MAX
local PIXEL_OFFSET = CB_RESULT_OFFSET + 8 * 8
local FIRST_WRITABLE = 8
local LAST_WRITABLE = 25
local STATUS_CALLBACK = 3
local CB_KIND_GETOBJECT = 0
local CB_KIND_LOADFIGURE = 1
local CB_KIND_EFFECT = 2
local CB_KIND_DRAW = 3

assert(loadfile(shimPath))()

local clamp = function(v, lo, hi)
    if v < lo then return lo end
    if v > hi then return hi end
    return v
end

local width, height = 0, 0
local pixels = ffi.cast("uint8_t*", base + PIXEL_OFFSET)
local cbResult = ffi.cast("double*", base + CB_RESULT_OFFSET)
local dirty = false

local obj = {}
local scene = {}
local ymm4 = {}

function obj.getpixel(x, y)
    x = math.floor(x); y = math.floor(y)
    if x < 0 or y < 0 or x >= width or y >= height then return 0, 0, 0, 0 end
    local p = pixels + (y * width + x) * 4
    local a = p[3]
    if a <= 0 then return 0, 0, 0, 0 end
    local s = 255 / a
    return clamp(p[2] * s, 0, 255), clamp(p[1] * s, 0, 255), clamp(p[0] * s, 0, 255), a
end

function obj.setpixel(x, y, r, g, b, a)
    x = math.floor(x); y = math.floor(y)
    if x < 0 or y < 0 or x >= width or y >= height then return end
    a = a or 255
    dirty = true
    local aK = clamp(a, 0, 255) / 255
    local p = pixels + (y * width + x) * 4
    p[0] = math.floor(clamp(b * aK, 0, 255))
    p[1] = math.floor(clamp(g * aK, 0, 255))
    p[2] = math.floor(clamp(r * aK, 0, 255))
    p[3] = math.floor(clamp(a, 0, 255))
end

function obj.getpixeldata()
    local total = width * height * 4
    local buf = pixels
    local pd = { width = width, height = height }
    function pd:get(index)
        local zb = index - 1
        if zb < 0 or zb >= total then return 0 end
        local pi = math.floor(zb / 4)
        local ch = zb - pi * 4
        local p = buf + pi * 4
        local a = p[3]
        if ch == 3 then return a end
        if a <= 0 then return 0 end
        local sc = 255 / a
        if ch == 0 then return clamp(p[2] * sc, 0, 255) end
        if ch == 1 then return clamp(p[1] * sc, 0, 255) end
        return clamp(p[0] * sc, 0, 255)
    end
    function pd:set(index, value)
        local zb = index - 1
        if zb < 0 or zb >= total then return end
        dirty = true
        local pi = math.floor(zb / 4)
        local ch = zb - pi * 4
        local p = buf + pi * 4
        local clamped = clamp(value, 0, 255)
        if ch == 3 then
            local oldA = p[3]
            local newA = clamped
            if oldA > 0 and newA > 0 then
                local f = newA / oldA
                p[0] = math.floor(clamp(p[0] * f, 0, 255))
                p[1] = math.floor(clamp(p[1] * f, 0, 255))
                p[2] = math.floor(clamp(p[2] * f, 0, 255))
            elseif newA <= 0 then
                p[0] = 0; p[1] = 0; p[2] = 0
            end
            p[3] = math.floor(newA)
        else
            local a = p[3]
            local pm = clamped * (a / 255)
            local bo = (ch == 0) and 2 or ((ch == 1) and 1 or 0)
            p[bo] = math.floor(clamp(pm, 0, 255))
        end
    end
    return pd
end

function obj.putpixeldata() end

function obj.rand(a, b, seed, frame)
    return aviutl_rand(a or 0, b or 0, seed or 0, frame or obj.frame or 0)
end

function obj.load(kind, name, color, size, lineWidth, aspect)
    if kind ~= "figure" then return end
    name = name or ""
    color = color or 0
    size = size or 100
    lineWidth = lineWidth or 0
    aspect = aspect or 0
    local nameBytes = tostring(name)
    local len = #nameBytes
    if len > CB_TAG_MAX then len = CB_TAG_MAX end
    ffi.copy(base + CB_TAG_OFFSET, nameBytes, len)
    i32[OFF_CB_TAGLEN] = len
    i32[OFF_CB_KIND] = CB_KIND_LOADFIGURE
    cbResult[0] = color
    cbResult[1] = size
    cbResult[2] = lineWidth
    cbResult[3] = aspect
    i32[OFF_STATUS] = STATUS_CALLBACK
    k32.SetEvent(doneEvent)
    k32.WaitForSingleObject(workEvent, INFINITE)
    local newW = i32[OFF_LOAD_RESULT_W]
    local newH = i32[OFF_LOAD_RESULT_H]
    if newW > 0 and newH > 0 then
        width = newW
        height = newH
        i32[OFF_WIDTH] = newW
        i32[OFF_HEIGHT] = newH
        dirty = true
        obj.w = newW; obj.h = newH
        obj.hw = newW / 2; obj.hh = newH / 2
        obj.cx = newW / 2; obj.cy = newH / 2
        obj.cz = 0
        obj.diagonal = math.sqrt(newW * newW + newH * newH)
    end
end

function obj.effect(name, ...)
    if type(name) ~= "string" then return end
    local args = { ... }
    local n = select("#", ...)
    local parts = { name }
    local pos = #name + 1
    for i = 1, n - 1, 2 do
        local k = tostring(args[i])
        local v = args[i + 1]
        local encoded
        local vt = type(v)
        if vt == "number" then
            encoded = "n" .. string.format("%.17g", v)
        elseif vt == "boolean" then
            encoded = v and "b1" or "b0"
        else
            encoded = "s" .. tostring(v)
        end
        local needed = 2 + #k + #encoded
        if pos + needed > CB_TAG_MAX then break end
        parts[#parts + 1] = k
        parts[#parts + 1] = encoded
        pos = pos + needed
    end
    local payload = table.concat(parts, "\0")
    local len = #payload
    if len > CB_TAG_MAX then len = CB_TAG_MAX end
    ffi.copy(base + CB_TAG_OFFSET, payload, len)
    i32[OFF_CB_TAGLEN] = len
    i32[OFF_CB_KIND] = CB_KIND_EFFECT
    i32[OFF_STATUS] = STATUS_CALLBACK
    k32.SetEvent(doneEvent)
    k32.WaitForSingleObject(workEvent, INFINITE)
end

function obj.draw(ox, oy, oz, zoom, alpha, aspect)
    cbResult[0] = ox or 0
    cbResult[1] = oy or 0
    cbResult[2] = oz or 0
    cbResult[3] = zoom or 1
    cbResult[4] = alpha or 1
    cbResult[5] = aspect or 0
    i32[OFF_CB_KIND] = CB_KIND_DRAW
    i32[OFF_STATUS] = STATUS_CALLBACK
    k32.SetEvent(doneEvent)
    k32.WaitForSingleObject(workEvent, INFINITE)
end

local cacheTag, cacheFrame, cacheFound
local cacheExist, cacheX, cacheY, cacheZ, cacheZoom, cacheRz, cacheAlpha, cacheLayer

local function buildObject()
    return {
        exist = cacheExist,
        x = cacheX, y = cacheY, z = cacheZ,
        zoom = cacheZoom, sx = cacheZoom, sy = cacheZoom,
        rx = 0, ry = 0, rz = cacheRz,
        rxr = 0, ryr = 0, rzr = cacheRz * math.pi / 180,
        alpha = cacheAlpha, layer = cacheLayer,
    }
end

function obj.getobject(tag, frame)
    if type(tag) ~= "string" then return nil end
    if frame then frame = math.floor(frame) else frame = math.floor(obj.timelineframe or 0) end
    if tag == cacheTag and frame == cacheFrame then
        if cacheFound then return buildObject() end
        return nil
    end
    local len = #tag
    if len > CB_TAG_MAX then len = CB_TAG_MAX end
    ffi.copy(base + CB_TAG_OFFSET, tag, len)
    i32[OFF_CB_TAGLEN] = len
    i32[OFF_CB_FRAME] = frame
    i32[OFF_CB_KIND] = CB_KIND_GETOBJECT
    i32[OFF_STATUS] = STATUS_CALLBACK
    k32.SetEvent(doneEvent)
    k32.WaitForSingleObject(workEvent, INFINITE)
    cacheTag = tag
    cacheFrame = frame
    if i32[OFF_CB_FOUND] == 0 then
        cacheFound = false
        return nil
    end
    cacheFound = true
    cacheExist = cbResult[0] ~= 0
    cacheX = cbResult[1]; cacheY = cbResult[2]; cacheZ = cbResult[3]
    cacheZoom = cbResult[4]; cacheRz = cbResult[5]; cacheAlpha = cbResult[6]; cacheLayer = cbResult[7]
    return buildObject()
end

local sandbox = {
    obj = obj, scene = scene, ymm4 = ymm4,
    anim = anim, bit32 = bit32, rawlen = rawlen,
    math = math, string = string, table = table,
    type = type, tostring = tostring, tonumber = tonumber, select = select,
    error = error, assert = assert, ipairs = ipairs, pairs = pairs, next = next,
    unpack = unpack, setmetatable = setmetatable, getmetatable = getmetatable,
    rawget = rawget, rawset = rawset, rawequal = rawequal, rawlen = rawlen,
    pcall = pcall, xpcall = xpcall, print = function() end,
}
sandbox._G = sandbox

local compiledCode, compiledChunk

local function loadFields()
    width = i32[OFF_WIDTH]
    height = i32[OFF_HEIGHT]
    obj.w = f64[0]; obj.h = f64[1]; obj.hw = f64[2]; obj.hh = f64[3]
    obj.cx = f64[4]; obj.cy = f64[5]; obj.cz = f64[6]; obj.diagonal = f64[7]
    obj.x = f64[8]; obj.y = f64[9]; obj.z = f64[10]
    obj.ox = f64[11]; obj.oy = f64[12]; obj.oz = f64[13]
    obj.sx = f64[14]; obj.sy = f64[15]; obj.sz = f64[16]
    obj.zoom = f64[17]; obj.aspect = f64[18]; obj.alpha = f64[19]
    obj.rx = f64[20]; obj.ry = f64[21]; obj.rz = f64[22]
    obj.rxr = f64[23]; obj.ryr = f64[24]; obj.rzr = f64[25]
    obj.track0 = f64[26]; obj.track1 = f64[27]; obj.track2 = f64[28]; obj.track3 = f64[29]
    obj.time = f64[30]; obj.frame = f64[31]; obj.totalframe = f64[32]; obj.totaltime = f64[33]
    obj.t = f64[34]; obj.framerate = f64[35]; obj.layer = f64[36]; obj.index = f64[37]; obj.num = f64[38]
    scene.width = f64[39]; scene.height = f64[40]; scene.cx = f64[41]; scene.cy = f64[42]
    ymm4.group_index = f64[43]; ymm4.group_count = f64[44]
    ymm4.timeline_totalframe = f64[45]; ymm4.timeline_totaltime = f64[46]
    ymm4.time_ratio = f64[47]
    ymm4.is_saving = f64[48] ~= 0; ymm4.is_playing = f64[49] ~= 0; ymm4.is_paused = f64[50] ~= 0
    ymm4.group_ratio = (f64[44] ~= 0) and (f64[43] / f64[44]) or 0
    obj.timelineframe = f64[51]; obj.timelinetime = f64[52]
    obj.check0 = f64[53] ~= 0; obj.check1 = f64[54] ~= 0; obj.check2 = f64[55] ~= 0; obj.check3 = f64[56] ~= 0
    sandbox.color = (f64[57] >= 0) and f64[57] or nil
end

local function storeFields()
    for i = FIRST_WRITABLE, LAST_WRITABLE do
        local key
        if i == 8 then key = obj.x elseif i == 9 then key = obj.y elseif i == 10 then key = obj.z
        elseif i == 11 then key = obj.ox elseif i == 12 then key = obj.oy elseif i == 13 then key = obj.oz
        elseif i == 14 then key = obj.sx elseif i == 15 then key = obj.sy elseif i == 16 then key = obj.sz
        elseif i == 17 then key = obj.zoom elseif i == 18 then key = obj.aspect elseif i == 19 then key = obj.alpha
        elseif i == 20 then key = obj.rx elseif i == 21 then key = obj.ry elseif i == 22 then key = obj.rz
        elseif i == 23 then key = obj.rxr elseif i == 24 then key = obj.ryr elseif i == 25 then key = obj.rzr end
        f64[i] = tonumber(key) or f64[i]
    end
end

while true do
    k32.WaitForSingleObject(workEvent, INFINITE)
    if i32[OFF_COMMAND] == 1 then break end

    dirty = false
    local scriptLen = i32[OFF_SCRIPTLEN]
    local code = ffi.string(base + SCRIPT_OFFSET, scriptLen)

    if code ~= compiledCode then
        local chunk, err = loadstring(code, "LuaScript")
        if chunk then
            compiledChunk = chunk
            compiledCode = code
        else
            compiledChunk = nil
            local msg = err or "compile error"
            ffi.copy(base + ERROR_OFFSET, msg, math.min(#msg, 4096))
            i32[OFF_ERRORLEN] = math.min(#msg, 4096)
            i32[OFF_STATUS] = 2
            k32.SetEvent(doneEvent)
        end
    end

    if compiledChunk and code == compiledCode then
        loadFields()
        cacheTag = nil
        math.randomseed(f64[31])
        setfenv(compiledChunk, sandbox)
        local ok, err = pcall(compiledChunk)
        if ok then
            storeFields()
            i32[OFF_PIXELSDIRTY] = dirty and 1 or 0
            i32[OFF_STATUS] = 1
            k32.SetEvent(doneEvent)
        else
            local msg = tostring(err)
            ffi.copy(base + ERROR_OFFSET, msg, math.min(#msg, 4096))
            i32[OFF_ERRORLEN] = math.min(#msg, 4096)
            i32[OFF_STATUS] = 2
            k32.SetEvent(doneEvent)
        end
    end
end

k32.UnmapViewOfFile(view)
k32.CloseHandle(hMap)
