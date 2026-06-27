local ffi = require("ffi")
local bit = require("bit")

local i64 = ffi.typeof("int64_t")
local u32 = ffi.typeof("uint32_t")
local u64 = ffi.typeof("uint64_t")

local function tou(d)
    return tonumber(u32(i64(d)))
end

local function umul32(a, b)
    return tonumber(u32(u64(a) * u64(b)))
end

local function mask(width)
    if width <= 0 then return 0 end
    if width >= 32 then return tou(0xFFFFFFFF) end
    return bit.lshift(1, width) - 1
end

local function validate(pos, width)
    if pos < 0 or width < 1 or pos + width > 32 then
        error("trying to access non-existent bits")
    end
end

bit32 = {}

function bit32.band(...) return tou(bit.band(...)) end
function bit32.bor(...) return tou(bit.bor(...)) end
function bit32.bxor(...) return tou(bit.bxor(...)) end
function bit32.btest(...) return tou(bit.band(...)) ~= 0 end
function bit32.bnot(x) return tou(bit.bnot(x)) end

function bit32.lshift(x, n)
    if n <= -32 or n >= 32 then return 0 end
    if n >= 0 then return tou(bit.lshift(x, n)) end
    return tou(bit.rshift(x, -n))
end

function bit32.rshift(x, n) return bit32.lshift(x, -n) end

function bit32.arshift(x, n)
    if n < 0 then return bit32.lshift(x, -n) end
    if n > 31 then n = 31 end
    return tou(bit.arshift(x, n))
end

function bit32.lrotate(x, n) return tou(bit.rol(x, n)) end
function bit32.rrotate(x, n) return tou(bit.ror(x, n)) end

function bit32.extract(v, pos, width)
    width = width or 1
    validate(pos, width)
    return tou(bit.band(bit.rshift(v, pos), mask(width)))
end

function bit32.replace(v, u, pos, width)
    width = width or 1
    validate(pos, width)
    local m = bit.lshift(mask(width), pos)
    return tou(bit.bor(bit.band(v, bit.bnot(m)), bit.band(bit.lshift(u, pos), m)))
end

if rawlen == nil then
    rawlen = function(x)
        local t = type(x)
        if t == "table" then return #x end
        if t == "string" then return string.len(x) end
        error("table or string expected")
    end
end

local floor = math.floor
local abs = math.abs
local sqrt = math.sqrt
local sin = math.sin
local cos = math.cos
local fmod = math.fmod
local pi = math.pi
local tau = pi * 2

local function clamp(v, lo, hi)
    if v < lo then return lo end
    if v > hi then return hi end
    return v
end

anim = {}
anim.tau = tau
anim.e = math.exp(1)
anim.phi = (1 + sqrt(5)) / 2
anim.sqrt2 = sqrt(2)

function anim.lerp(a, b, t) return a + (b - a) * t end

function anim.smoothstep(e0, e1, x)
    local span = e1 - e0
    local t = span == 0 and 0 or clamp((x - e0) / span, 0, 1)
    return t * t * (3 - 2 * t)
end

function anim.smootherstep(e0, e1, x)
    local span = e1 - e0
    local t = span == 0 and 0 or clamp((x - e0) / span, 0, 1)
    return t * t * t * (t * (6 * t - 15) + 10)
end

function anim.clamp(v, lo, hi) return clamp(v, lo, hi) end

function anim.map(v, a1, b1, a2, b2)
    local range = b1 - a1
    if range == 0 then return a2 end
    return a2 + (b2 - a2) * (v - a1) / range
end

function anim.norm(v, lo, hi)
    local span = hi - lo
    if span == 0 then return 0 end
    return (v - lo) / span
end

function anim.wrap(v, lo, hi)
    local range = hi - lo
    if range <= 0 then return lo end
    local result = fmod(v - lo, range)
    if result < 0 then result = result + range end
    return lo + result
end

function anim.pingpong(t, length)
    if length <= 0 then return 0 end
    local v = fmod(t, 2 * length)
    if v < 0 then v = v + 2 * length end
    if v > length then return 2 * length - v end
    return v
end

function anim.sign(v)
    if v > 0 then return 1 end
    if v < 0 then return -1 end
    return 0
end

function anim.oscillate(t, lo, hi, freq)
    local wave = (sin(t * freq * tau) + 1) / 2
    return lo + (hi - lo) * wave
end

function anim.triangle(t, freq)
    local f = t * freq
    f = f - floor(f)
    return 1 - 2 * abs(f - 0.5)
end

function anim.square(t, freq)
    local f = t * freq
    f = f - floor(f)
    return f >= 0.5 and 1 or 0
end

function anim.duration(t, dur)
    if dur <= 0 then return 1 end
    return clamp(t / dur, 0, 1)
end

function anim.delay(t, d) return math.max(0, t - d) end

function anim.ease_in(t) return t * t end

function anim.ease_out(t)
    local inv = 1 - t
    return 1 - inv * inv
end

function anim.ease_in_out(t)
    if t < 0.5 then return 2 * t * t end
    local inv = -2 * t + 2
    return 1 - inv * inv / 2
end

function anim.elastic(t)
    t = clamp(t, 0, 1)
    if t == 0 then return 0 end
    if t == 1 then return 1 end
    return 2 ^ (-10 * t) * sin((t * 10 - 0.75) * (tau / 3)) + 1
end

function anim.back(t)
    local c1 = 1.70158
    local c3 = c1 + 1
    local inv = t - 1
    return 1 + c3 * inv * inv * inv + c1 * inv * inv
end

function anim.step(edge, x) return x >= edge and 1 or 0 end

function anim.fract(v) return v - floor(v) end

function anim.bounce(t)
    local n1 = 7.5625
    local d1 = 2.75
    t = clamp(t, 0, 1)
    if t < 1 / d1 then return n1 * t * t end
    if t < 2 / d1 then
        t = t - 1.5 / d1
        return n1 * t * t + 0.75
    end
    if t < 2.5 / d1 then
        t = t - 2.25 / d1
        return n1 * t * t + 0.9375
    end
    t = t - 2.625 / d1
    return n1 * t * t + 0.984375
end

function anim.hsv_to_rgb(h, s, v)
    local c = v * s
    local x = c * (1 - abs(fmod(h / 60, 2) - 1))
    local m = v - c
    local sector = floor(h / 60) % 6
    local r, g, b
    if sector == 0 then r, g, b = c, x, 0
    elseif sector == 1 then r, g, b = x, c, 0
    elseif sector == 2 then r, g, b = 0, c, x
    elseif sector == 3 then r, g, b = 0, x, c
    elseif sector == 4 then r, g, b = x, 0, c
    else r, g, b = c, 0, x end
    return (r + m) * 255, (g + m) * 255, (b + m) * 255
end

function anim.rgb_to_hsv(r, g, b)
    r = r / 255; g = g / 255; b = b / 255
    local max = math.max(r, g, b)
    local min = math.min(r, g, b)
    local delta = max - min
    local h = 0
    if delta > 0 then
        if max == r then
            h = 60 * fmod(fmod((g - b) / delta, 6) + 6, 6)
        elseif max == g then
            h = 60 * ((b - r) / delta + 2)
        else
            h = 60 * ((r - g) / delta + 4)
        end
    end
    return h, max > 0 and delta / max or 0, max
end

function anim.len(x, y) return sqrt(x * x + y * y) end

function anim.dist(x1, y1, x2, y2)
    local dx = x2 - x1
    local dy = y2 - y1
    return sqrt(dx * dx + dy * dy)
end

function anim.dot(x1, y1, x2, y2) return x1 * x2 + y1 * y2 end

function anim.normalize(x, y)
    local m = sqrt(x * x + y * y)
    if m == 0 then return 0, 0 end
    return x / m, y / m
end

function anim.polar(r, a)
    local rad = a * pi / 180
    return r * cos(rad), r * sin(rad)
end

function anim.rotate(x, y, a)
    local rad = a * pi / 180
    local c = cos(rad)
    local s = sin(rad)
    return x * c - y * s, x * s + y * c
end

function anim.bezier(t, p0, p1, p2, p3)
    t = clamp(t, 0, 1)
    local inv = 1 - t
    local b0 = inv * inv * inv
    local b1 = 3 * inv * inv * t
    local b2 = 3 * inv * t * t
    local b3 = t * t * t
    return b0 * p0 + b1 * p1 + b2 * p2 + b3 * p3
end

function anim.rand(...)
    local a = { ... }
    local n = select("#", ...)
    if n == 0 then return 0 end
    local min, max, seed = 0, 1, 0
    if n == 1 then
        seed = a[1]
    elseif n == 2 then
        max, seed = a[1], a[2]
    else
        min, max, seed = a[1], a[2], a[3]
    end
    local p = ffi.new("double[1]", seed)
    local w = ffi.cast("uint32_t*", p)
    local lo, hi = tonumber(w[0]), tonumber(w[1])
    local h = umul32(tou(bit.bxor(lo, hi)), 374761393)
    h = umul32(tou(bit.bxor(h, bit.rshift(h, 13))), 1274126177)
    local r = tou(bit.bxor(h, bit.rshift(h, 16))) / 4294967295.0
    return min + r * (max - min)
end

local function noise_hash(x, y, z)
    local n = tou(umul32(tou(x), 374761393) + umul32(tou(y), 668265263) + umul32(tou(z), 1013904223))
    n = umul32(tou(bit.bxor(n, bit.rshift(n, 13))), 1274126177)
    return tou(bit.bxor(n, bit.rshift(n, 16))) / 4294967295.0
end

function anim.noise(x, y, z)
    y = y or 0
    z = z or 0
    local xi = floor(x)
    local yi = floor(y)
    local zi = floor(z)
    local xf = x - xi
    local yf = y - yi
    local zf = z - zi
    local u = xf * xf * (3 - 2 * xf)
    local v = yf * yf * (3 - 2 * yf)
    local w = zf * zf * (3 - 2 * zf)

    local c000 = noise_hash(xi, yi, zi)
    local c100 = noise_hash(xi + 1, yi, zi)
    local c010 = noise_hash(xi, yi + 1, zi)
    local c110 = noise_hash(xi + 1, yi + 1, zi)
    local c001 = noise_hash(xi, yi, zi + 1)
    local c101 = noise_hash(xi + 1, yi, zi + 1)
    local c011 = noise_hash(xi, yi + 1, zi + 1)
    local c111 = noise_hash(xi + 1, yi + 1, zi + 1)

    local x00 = c000 + u * (c100 - c000)
    local x10 = c010 + u * (c110 - c010)
    local x01 = c001 + u * (c101 - c001)
    local x11 = c011 + u * (c111 - c011)

    local y0 = x00 + v * (x10 - x00)
    local y1 = x01 + v * (x11 - x01)

    return y0 + w * (y1 - y0)
end
