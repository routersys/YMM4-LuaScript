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
local stringParamsCap = tonumber(arg[6])

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
local OFF_STRING_PARAMS_LEN = 14
local OFF_SCRIPT_VERSION = 15
local SCRIPT_OFFSET = 64 + 64 * 8
local ERROR_OFFSET = SCRIPT_OFFSET + 128 * 1024
local CB_TAG_OFFSET = ERROR_OFFSET + 4 * 1024
local CB_TAG_MAX = 4096
local CB_RESULT_OFFSET = CB_TAG_OFFSET + CB_TAG_MAX
local STRING_PARAMS_OFFSET = CB_RESULT_OFFSET + 8 * 8
local DRAW_RING_CAPACITY = 4096
local DRAW_ENTRY_DOUBLES = 24
local DRAW_RING_DOUBLES = 1 + DRAW_RING_CAPACITY * DRAW_ENTRY_DOUBLES
local DRAW_RING_BYTES = DRAW_RING_DOUBLES * 8
local DRAW_RING_OFFSET = STRING_PARAMS_OFFSET + stringParamsCap
local PIXEL_OFFSET = DRAW_RING_OFFSET + DRAW_RING_BYTES
local FIRST_WRITABLE = 8
local LAST_WRITABLE = 25
local STATUS_CALLBACK = 3
local CB_KIND_GETOBJECT = 0
local CB_KIND_LOADFIGURE = 1
local CB_KIND_EFFECT = 2
local CB_KIND_DRAW = 3
local CB_KIND_DRAWPOLY = 4
local CB_KIND_LOADTEXT = 5
local CB_KIND_LOADIMAGE = 6
local CB_KIND_LOADMOVIE = 7
local CB_KIND_SETANCHOR = 8
local CB_KIND_REQUESTPIXELS = 9
local CB_KIND_FLUSHDRAWS = 10

assert(loadfile(shimPath))()

local clamp = function(v, lo, hi)
    if v < lo then return lo end
    if v > hi then return hi end
    return v
end

local width, height = 0, 0
local pixels = ffi.cast("uint8_t*", base + PIXEL_OFFSET)
local ringD = ffi.cast("double*", base + DRAW_RING_OFFSET)
local cbResult = ffi.cast("double*", base + CB_RESULT_OFFSET)
local cbTagD = ffi.cast("double*", base + CB_TAG_OFFSET)
local pixelCapacity = mapSize - PIXEL_OFFSET
local buffers = {}
local options = {}
local pixeloptions = {}
local dirty = false
local pixelsValid = false
local drawTarget = "frame"
local pdData, pdCapacity, pdValid, pdDirty = nil, 0, false, false

local function ensurePixels()
    if pixelsValid then return end
    i32[OFF_CB_KIND] = CB_KIND_REQUESTPIXELS
    i32[OFF_STATUS] = STATUS_CALLBACK
    k32.SetEvent(doneEvent)
    k32.WaitForSingleObject(workEvent, INFINITE)
    pixelsValid = true
end

local function flushDraws()
    i32[OFF_CB_KIND] = CB_KIND_FLUSHDRAWS
    i32[OFF_STATUS] = STATUS_CALLBACK
    k32.SetEvent(doneEvent)
    k32.WaitForSingleObject(workEvent, INFINITE)
end

local function ringSlot(kind)
    local n = ringD[0]
    if n >= DRAW_RING_CAPACITY then
        flushDraws()
        n = 0
    end
    local eb = 1 + n * DRAW_ENTRY_DOUBLES
    ringD[eb] = kind
    ringD[0] = n + 1
    return eb
end

local function rebuildPixelData()
    local count = width * height * 4
    if not pdData or pdCapacity < count then
        pdData = ffi.new("double[?]", count)
        pdCapacity = count
    end
    for pi = 0, width * height - 1 do
        local p = pixels + pi * 4
        local di = pi * 4
        local a = p[3]
        pdData[di + 3] = a
        if a <= 0 then
            pdData[di] = 0; pdData[di + 1] = 0; pdData[di + 2] = 0
        else
            local s = 255 / a
            pdData[di] = clamp(p[2] * s, 0, 255)
            pdData[di + 1] = clamp(p[1] * s, 0, 255)
            pdData[di + 2] = clamp(p[0] * s, 0, 255)
        end
    end
    pdValid = true
    pdDirty = false
end

local function flushPixelData()
    if not pdDirty then return end
    for pi = 0, width * height - 1 do
        local di = pi * 4
        local a = math.floor(clamp(pdData[di + 3], 0, 255))
        local aK = a / 255
        local p = pixels + pi * 4
        p[0] = math.floor(clamp(pdData[di + 2] * aK, 0, 255))
        p[1] = math.floor(clamp(pdData[di + 1] * aK, 0, 255))
        p[2] = math.floor(clamp(pdData[di] * aK, 0, 255))
        p[3] = a
    end
    dirty = true
    pdDirty = false
end

local function sampleSource(src, srcW, srcH, u, v, linear)
    if not linear then
        local su = clamp(math.floor(u), 0, srcW - 1)
        local sv = clamp(math.floor(v), 0, srcH - 1)
        local si = (sv * srcW + su) * 4
        return src[si], src[si + 1], src[si + 2], src[si + 3]
    end
    local fx = u - 0.5
    local fy = v - 0.5
    local x0 = math.floor(fx)
    local y0 = math.floor(fy)
    local tx = fx - x0
    local ty = fy - y0
    local b, g, r, a = 0, 0, 0, 0
    for j = 0, 1 do
        local sy = clamp(y0 + j, 0, srcH - 1)
        local wy = (j == 0) and (1 - ty) or ty
        for i = 0, 1 do
            local sx = clamp(x0 + i, 0, srcW - 1)
            local w = wy * ((i == 0) and (1 - tx) or tx)
            local si = (sy * srcW + sx) * 4
            b = b + src[si] * w
            g = g + src[si + 1] * w
            r = r + src[si + 2] * w
            a = a + src[si + 3] * w
        end
    end
    return b, g, r, a
end

local function compositeOver(dst, di, sb, sg, sr, sa)
    local inv = 1 - sa / 255
    dst[di] = math.floor(clamp(sb + dst[di] * inv, 0, 255))
    dst[di + 1] = math.floor(clamp(sg + dst[di + 1] * inv, 0, 255))
    dst[di + 2] = math.floor(clamp(sr + dst[di + 2] * inv, 0, 255))
    dst[di + 3] = math.floor(clamp(sa + dst[di + 3] * inv, 0, 255))
end

local function compositeDrawInto(dst, dstW, dstH, src, srcW, srcH, ox, oy, zoom, aspect, alpha, linear)
    if srcW <= 0 or srcH <= 0 or dstW <= 0 or dstH <= 0 then return end
    local ca = clamp(aspect, -1, 1)
    local zx = zoom * (1 + ca)
    local zy = zoom * (1 - ca)
    if zx == 0 or zy == 0 then return end
    local ka = clamp(alpha, 0, 1)
    if ka <= 0 then return end
    local halfW = srcW * 0.5
    local halfH = srcH * 0.5
    local spanX = math.abs(halfW * zx)
    local spanY = math.abs(halfH * zy)
    local x0 = math.max(0, math.floor(ox - spanX))
    local x1 = math.min(dstW - 1, math.ceil(ox + spanX))
    local y0 = math.max(0, math.floor(oy - spanY))
    local y1 = math.min(dstH - 1, math.ceil(oy + spanY))
    for py = y0, y1 do
        for px = x0, x1 do
            local u = (px + 0.5 - ox) / zx + halfW
            local v = (py + 0.5 - oy) / zy + halfH
            if u >= 0 and u < srcW and v >= 0 and v < srcH then
                local sb, sg, sr, sa = sampleSource(src, srcW, srcH, u, v, linear)
                compositeOver(dst, (py * dstW + px) * 4, sb * ka, sg * ka, sr * ka, sa * ka)
            end
        end
    end
end

local function compositeDrawPolyInto(dst, dstW, dstH, src, srcW, srcH, poly, alpha, linear)
    if srcW <= 0 or srcH <= 0 or dstW <= 0 or dstH <= 0 then return end
    local x0p, y0p = poly[0], poly[1]
    local x1p, y1p = poly[3], poly[4]
    local x3p, y3p = poly[9], poly[10]
    local u0, v0 = poly[12], poly[13]
    local u1, v1 = poly[14], poly[15]
    local u3, v3 = poly[18], poly[19]
    local det = u0 * (v1 - v3) - v0 * (u1 - u3) + (u1 * v3 - v1 * u3)
    if math.abs(det) < 1e-9 then return end
    local inv = 1 / det
    local a = (x0p * (v1 - v3) - v0 * (x1p - x3p) + (x1p * v3 - v1 * x3p)) * inv
    local b = (u0 * (x1p - x3p) - x0p * (u1 - u3) + (u1 * x3p - x1p * u3)) * inv
    local c = (u0 * (v1 * x3p - x1p * v3) - v0 * (u1 * x3p - x1p * u3) + x0p * (u1 * v3 - v1 * u3)) * inv
    local d = (y0p * (v1 - v3) - v0 * (y1p - y3p) + (y1p * v3 - v1 * y3p)) * inv
    local e = (u0 * (y1p - y3p) - y0p * (u1 - u3) + (u1 * y3p - y1p * u3)) * inv
    local f = (u0 * (v1 * y3p - y1p * v3) - v0 * (u1 * y3p - y1p * u3) + y0p * (u1 * v3 - v1 * u3)) * inv
    local adet = a * e - b * d
    if math.abs(adet) < 1e-9 then return end
    local ka = clamp(alpha, 0, 1)
    if ka <= 0 then return end
    local minX, minY, maxX, maxY = poly[0], poly[1], poly[0], poly[1]
    for i = 0, 3 do
        local xx = poly[i * 3]
        local yy = poly[i * 3 + 1]
        if xx < minX then minX = xx end
        if xx > maxX then maxX = xx end
        if yy < minY then minY = yy end
        if yy > maxY then maxY = yy end
    end
    local bx0 = math.max(0, math.floor(minX))
    local bx1 = math.min(dstW - 1, math.ceil(maxX))
    local by0 = math.max(0, math.floor(minY))
    local by1 = math.min(dstH - 1, math.ceil(maxY))
    for py = by0, by1 do
        for px = bx0, bx1 do
            local dx = (px + 0.5) - c
            local dy = (py + 0.5) - f
            local u = (e * dx - b * dy) / adet
            local v = (-d * dx + a * dy) / adet
            if u >= 0 and u < srcW and v >= 0 and v < srcH then
                local sb, sg, sr, sa = sampleSource(src, srcW, srcH, u, v, linear)
                compositeOver(dst, (py * dstW + px) * 4, sb * ka, sg * ka, sr * ka, sa * ka)
            end
        end
    end
end

local function ensureTemp()
    local t = buffers["t"]
    if not t then
        local n = width * height * 4
        t = { data = ffi.new("uint8_t[?]", n), w = width, h = height }
        buffers["t"] = t
    end
    return t
end

local function bufferKind(id)
    local c = id:match("^%s*(.)")
    if not c then return nil end
    c = c:lower()
    if c == "c" then
        local name = id:match(":%s*(.-)%s*$")
        return "c", name and ("c:" .. name) or "c"
    end
    return c, c
end

local sandbox
local obj = {}
local scene = {}
local ymm4 = {}

function obj.getpixel(x, y)
    x = math.floor(x); y = math.floor(y)
    if x < 0 or y < 0 or x >= width or y >= height then return 0, 0, 0, 0 end
    ensurePixels()
    flushPixelData()
    local p = pixels + (y * width + x) * 4
    local a = p[3]
    if a <= 0 then return 0, 0, 0, 0 end
    local s = 255 / a
    return clamp(p[2] * s, 0, 255), clamp(p[1] * s, 0, 255), clamp(p[0] * s, 0, 255), a
end

function obj.setpixel(x, y, r, g, b, a)
    x = math.floor(x); y = math.floor(y)
    if x < 0 or y < 0 or x >= width or y >= height then return end
    ensurePixels()
    flushPixelData()
    a = a or 255
    dirty = true
    local aK = clamp(a, 0, 255) / 255
    local p = pixels + (y * width + x) * 4
    p[0] = math.floor(clamp(b * aK, 0, 255))
    p[1] = math.floor(clamp(g * aK, 0, 255))
    p[2] = math.floor(clamp(r * aK, 0, 255))
    p[3] = math.floor(clamp(a, 0, 255))
    pdValid = false
end

function obj.getpixeldata()
    ensurePixels()
    if not pdValid then rebuildPixelData() end
    local data = pdData
    local total = width * height * 4
    local pd = { width = width, height = height }
    function pd:get(index)
        local zb = index - 1
        if zb < 0 or zb >= total then return 0 end
        return data[zb]
    end
    function pd:set(index, value)
        local zb = index - 1
        if zb < 0 or zb >= total then return end
        data[zb] = clamp(value, 0, 255)
        pdDirty = true
    end
    return pd
end

function obj.putpixeldata() flushPixelData() end

function obj.rand(a, b, seed, frame)
    return aviutl_rand(a or 0, b or 0, seed or 0, frame or obj.frame or 0)
end

local font = { family = "", size = 34, bold = false, italic = false, color = 0xFFFFFF }

function obj.setfont(name, size, style, col1)
    if name ~= nil then font.family = tostring(name) end
    if size ~= nil then font.size = size end
    if style ~= nil then
        local s = math.floor(style)
        font.bold = (s % 2) == 1
        font.italic = (math.floor(s / 2) % 2) == 1
    end
    if col1 ~= nil then font.color = col1 end
end

local function applyLoadResult()
    local newW = i32[OFF_LOAD_RESULT_W]
    local newH = i32[OFF_LOAD_RESULT_H]
    if newW > 0 and newH > 0 then
        width = newW
        height = newH
        i32[OFF_WIDTH] = newW
        i32[OFF_HEIGHT] = newH
        dirty = true
        pixelsValid = true
        pdValid = false
        pdDirty = false
        obj.w = newW; obj.h = newH
        obj.hw = newW / 2; obj.hh = newH / 2
        obj.cx = newW / 2; obj.cy = newH / 2
        obj.cz = 0
        obj.diagonal = math.sqrt(newW * newW + newH * newH)
    end
end

local function loadFigure(name, color, size, lineWidth, aspect)
    local nameBytes = tostring(name or "")
    local len = #nameBytes
    if len > CB_TAG_MAX then len = CB_TAG_MAX end
    ffi.copy(base + CB_TAG_OFFSET, nameBytes, len)
    i32[OFF_CB_TAGLEN] = len
    i32[OFF_CB_KIND] = CB_KIND_LOADFIGURE
    cbResult[0] = color or 0
    cbResult[1] = size or 100
    cbResult[2] = lineWidth or 0
    cbResult[3] = aspect or 0
    i32[OFF_STATUS] = STATUS_CALLBACK
    k32.SetEvent(doneEvent)
    k32.WaitForSingleObject(workEvent, INFINITE)
    applyLoadResult()
end

local function loadText(str)
    local payload = font.family .. "\0" .. tostring(str or "")
    local len = #payload
    if len > CB_TAG_MAX then len = CB_TAG_MAX end
    ffi.copy(base + CB_TAG_OFFSET, payload, len)
    i32[OFF_CB_TAGLEN] = len
    i32[OFF_CB_KIND] = CB_KIND_LOADTEXT
    cbResult[0] = font.size
    cbResult[1] = font.bold and 1 or 0
    cbResult[2] = font.italic and 1 or 0
    cbResult[3] = font.color
    i32[OFF_STATUS] = STATUS_CALLBACK
    k32.SetEvent(doneEvent)
    k32.WaitForSingleObject(workEvent, INFINITE)
    applyLoadResult()
end

local function loadImage(path)
    local payload = tostring(path or "")
    local len = #payload
    if len > CB_TAG_MAX then len = CB_TAG_MAX end
    ffi.copy(base + CB_TAG_OFFSET, payload, len)
    i32[OFF_CB_TAGLEN] = len
    i32[OFF_CB_KIND] = CB_KIND_LOADIMAGE
    i32[OFF_STATUS] = STATUS_CALLBACK
    k32.SetEvent(doneEvent)
    k32.WaitForSingleObject(workEvent, INFINITE)
    applyLoadResult()
end

local function loadMovie(path, time)
    local payload = tostring(path or "")
    local len = #payload
    if len > CB_TAG_MAX then len = CB_TAG_MAX end
    ffi.copy(base + CB_TAG_OFFSET, payload, len)
    i32[OFF_CB_TAGLEN] = len
    i32[OFF_CB_KIND] = CB_KIND_LOADMOVIE
    cbResult[0] = time or obj.time or 0
    i32[OFF_STATUS] = STATUS_CALLBACK
    k32.SetEvent(doneEvent)
    k32.WaitForSingleObject(workEvent, INFINITE)
    applyLoadResult()
end

function obj.load(kind, ...)
    if kind == "figure" then
        loadFigure(...)
    elseif kind == "text" then
        loadText(...)
    elseif kind == "image" then
        loadImage(...)
    elseif kind == "movie" then
        loadMovie(...)
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
    if drawTarget == "temp" then
        ensurePixels()
        flushPixelData()
        local t = ensureTemp()
        compositeDrawInto(t.data, t.w, t.h, pixels, width, height,
            ox or 0, oy or 0, zoom or 1, aspect or 0, alpha or 1, (options.antialias or 1) ~= 0)
        return
    end
    local eb = ringSlot(CB_KIND_DRAW)
    ringD[eb + 1] = ox or 0
    ringD[eb + 2] = oy or 0
    ringD[eb + 3] = oz or 0
    ringD[eb + 4] = zoom or 1
    ringD[eb + 5] = alpha or 1
    ringD[eb + 6] = aspect or 0
    ringD[eb + 7] = options.antialias or 1
    ringD[eb + 8] = options.blend or 0
end

function obj.drawpoly(...)
    local n = select("#", ...)
    if n < 12 then return end
    local a = { ... }
    local p = {}
    for i = 0, 11 do p[i] = a[i + 1] or 0 end
    if n >= 20 then
        for i = 0, 7 do p[12 + i] = a[13 + i] or 0 end
    else
        p[12] = 0; p[13] = 0
        p[14] = width; p[15] = 0
        p[16] = width; p[17] = height
        p[18] = 0; p[19] = height
    end
    local alpha = 1
    if n == 13 then
        alpha = a[13] or 1
    elseif n >= 21 then
        alpha = a[21] or 1
    end
    p[20] = alpha
    if drawTarget == "temp" then
        ensurePixels()
        flushPixelData()
        local t = ensureTemp()
        compositeDrawPolyInto(t.data, t.w, t.h, pixels, width, height, p, alpha, (options.antialias or 1) ~= 0)
        return
    end
    local eb = ringSlot(CB_KIND_DRAWPOLY)
    for i = 0, 20 do ringD[eb + 1 + i] = p[i] end
    ringD[eb + 22] = options.antialias or 1
    ringD[eb + 23] = options.blend or 0
end

function obj.getvalue(target)
    if type(target) ~= "string" then return 0 end
    local v = obj[target]
    if type(v) == "number" then return v end
    return 0
end

function obj.setoption(name, value, a, b)
    if type(name) ~= "string" then return end
    if value == nil then value = true end
    options[name] = value
    if name == "drawtarget" then
        if value == "tempbuffer" then
            drawTarget = "temp"
            if a ~= nil and b ~= nil then
                local w = math.max(1, math.floor(a))
                local h = math.max(1, math.floor(b))
                buffers["t"] = { data = ffi.new("uint8_t[?]", w * h * 4), w = w, h = h }
            end
        else
            drawTarget = "frame"
        end
    end
end

function obj.getoption(name)
    if type(name) ~= "string" then return 0 end
    local v = options[name]
    if v == nil then return 0 end
    return v
end

function obj.pixeloption(name, value)
    if type(name) ~= "string" then return end
    if value == nil then value = true end
    pixeloptions[name] = value
end

function obj.setanchor(name, count, ...)
    if type(name) ~= "string" then return 0 end
    count = math.floor(count or 0)
    if count < 0 then count = 0 elseif count > 32 then count = 32 end
    if name == "track" then return 0 end

    local connection, is3d = 0, false
    local optCount = select("#", ...)
    for i = 1, optCount do
        local o = select(i, ...)
        if o == "line" then connection = 1
        elseif o == "loop" then connection = 2
        elseif o == "star" then connection = 3
        elseif o == "arm" then connection = 4
        elseif o == "xyz" then is3d = true end
    end

    local len = #name
    if len > CB_TAG_MAX then len = CB_TAG_MAX end
    ffi.copy(base + CB_TAG_OFFSET, name, len)
    i32[OFF_CB_TAGLEN] = len
    cbResult[0] = count
    cbResult[1] = is3d and 1 or 0
    cbResult[2] = connection
    i32[OFF_CB_KIND] = CB_KIND_SETANCHOR
    i32[OFF_STATUS] = STATUS_CALLBACK
    k32.SetEvent(doneEvent)
    k32.WaitForSingleObject(workEvent, INFINITE)

    local stride = is3d and 3 or 2
    local pos = {}
    for i = 0, count - 1 do
        pos[i * stride + 1] = cbTagD[i * stride + 0]
        pos[i * stride + 2] = cbTagD[i * stride + 1]
        if is3d then pos[i * stride + 3] = cbTagD[i * stride + 2] end
    end
    sandbox[name] = pos
    return count
end

function obj.copybuffer(dst, src)
    if type(dst) ~= "string" or type(src) ~= "string" then return end
    local sk, skey = bufferKind(src)
    local data, w, h
    if sk == "o" then
        ensurePixels()
        flushPixelData()
        data = pixels
        w = width; h = height
    elseif sk == "t" or sk == "c" then
        local b = buffers[skey]
        if not b then return end
        data, w, h = b.data, b.w, b.h
    else
        return
    end
    local dk, dkey = bufferKind(dst)
    if dk == "o" then
        local need = w * h * 4
        if need > pixelCapacity then return end
        ffi.copy(pixels, data, need)
        width = w; height = h
        i32[OFF_WIDTH] = w; i32[OFF_HEIGHT] = h
        dirty = true
        pixelsValid = true
        pdValid = false
        pdDirty = false
        obj.w = w; obj.h = h
        obj.hw = w / 2; obj.hh = h / 2
        obj.cx = w / 2; obj.cy = h / 2
        obj.cz = 0
        obj.diagonal = math.sqrt(w * w + h * h)
    elseif dk == "t" or dk == "c" then
        local n = w * h * 4
        local arr = ffi.new("uint8_t[?]", n)
        ffi.copy(arr, data, n)
        buffers[dkey] = { data = arr, w = w, h = h }
    end
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

sandbox = {
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

local function captureKeys(t)
    local keys = {}
    for k in pairs(t) do keys[k] = true end
    return keys
end

local function resetExtraKeys(t, keys)
    for k in pairs(t) do
        if not keys[k] then t[k] = nil end
    end
end

local sandboxKeys = captureKeys(sandbox)
local objKeys = captureKeys(obj)
local sceneKeys = captureKeys(scene)
local ymm4Keys = captureKeys(ymm4)

local compiledCode, compiledChunk, compiledError
local lastScriptVersion

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
    obj.slider0 = f64[58]; obj.slider1 = f64[59]; obj.slider2 = f64[60]; obj.slider3 = f64[61]
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
    sandbox.time = f64[30]; sandbox.frame = f64[31]; sandbox.totalframe = f64[32]
    sandbox.framerate = f64[35]; sandbox.layer = f64[36]
    sandbox.timelineframe = f64[51]; sandbox.timelinetime = f64[52]
end

local function loadStringParams()
    if i32[OFF_STRING_PARAMS_LEN] <= 0 then return end
    local p = base + STRING_PARAMS_OFFSET
    local count = ffi.cast("int32_t*", p)[0]
    local pos = 4
    for _ = 1, count do
        local nameLen = ffi.cast("int32_t*", p + pos)[0]; pos = pos + 4
        local name = ffi.string(p + pos, nameLen); pos = pos + nameLen
        local valLen = ffi.cast("int32_t*", p + pos)[0]; pos = pos + 4
        local val = ffi.string(p + pos, valLen); pos = pos + valLen
        obj[name] = val
    end
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
    pixelsValid = false
    pdValid = false
    pdDirty = false
    ringD[0] = 0
    local version = i32[OFF_SCRIPT_VERSION]
    if version ~= lastScriptVersion then
        lastScriptVersion = version
        local scriptLen = i32[OFF_SCRIPTLEN]
        local code = ffi.string(base + SCRIPT_OFFSET, scriptLen)
        if code ~= compiledCode then
            local chunk, err = loadstring(code, "LuaScript")
            if chunk then
                compiledChunk = chunk
                compiledCode = code
                compiledError = nil
            else
                compiledChunk = nil
                compiledCode = code
                compiledError = err or "compile error"
            end
        end
    end

    if not compiledChunk then
        local msg = compiledError or "compile error"
        ffi.copy(base + ERROR_OFFSET, msg, math.min(#msg, 4096))
        i32[OFF_ERRORLEN] = math.min(#msg, 4096)
        i32[OFF_STATUS] = 2
        k32.SetEvent(doneEvent)
    else
        resetExtraKeys(sandbox, sandboxKeys)
        resetExtraKeys(obj, objKeys)
        resetExtraKeys(scene, sceneKeys)
        resetExtraKeys(ymm4, ymm4Keys)
        loadFields()
        loadStringParams()
        cacheTag = nil
        for k in pairs(options) do options[k] = nil end
        for k in pairs(pixeloptions) do pixeloptions[k] = nil end
        drawTarget = "frame"
        font.family = ""; font.size = 34; font.bold = false; font.italic = false; font.color = 0xFFFFFF
        math.randomseed(f64[31])
        setfenv(compiledChunk, sandbox)
        local ok, err = pcall(compiledChunk)
        if ok then
            flushPixelData()
            storeFields()
            local ds = options.draw_state
            if ds == nil then f64[62] = 0 elseif ds == false or ds == 0 then f64[62] = 2 else f64[62] = 1 end
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
