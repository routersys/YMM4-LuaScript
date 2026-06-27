local ffi = require("ffi")

ffi.cdef [[
typedef void* HANDLE;
HANDLE OpenFileMappingA(unsigned long dwDesiredAccess, int bInheritHandle, const char* lpName);
void* MapViewOfFile(HANDLE hFileMappingObject, unsigned long dwDesiredAccess, unsigned long dwFileOffsetHigh, unsigned long dwFileOffsetLow, size_t dwNumberOfBytesToMap);
int UnmapViewOfFile(const void* lpBaseAddress);
int CloseHandle(HANDLE hObject);
unsigned long GetLastError();
]]

local k32 = ffi.load("kernel32")
local FILE_MAP_ALL_ACCESS = 0x000F001F

local name = arg[1]
local size = tonumber(arg[2])

local h = k32.OpenFileMappingA(FILE_MAP_ALL_ACCESS, 0, name)
if h == nil then error("OpenFileMapping failed: " .. tonumber(k32.GetLastError())) end

local p = k32.MapViewOfFile(h, FILE_MAP_ALL_ACCESS, 0, 0, size)
if p == nil then
    k32.CloseHandle(h)
    error("MapViewOfFile failed: " .. tonumber(k32.GetLastError()))
end

local buf = ffi.cast("uint8_t*", p)
local pixels = size / 4
for i = 0, pixels - 1 do
    local b = buf[i * 4]
    local g = buf[i * 4 + 1]
    local r = buf[i * 4 + 2]
    local gray = math.floor(r * 0.299 + g * 0.587 + b * 0.114 + 0.5)
    if gray > 255 then gray = 255 end
    buf[i * 4] = gray
    buf[i * 4 + 1] = gray
    buf[i * 4 + 2] = gray
end

k32.UnmapViewOfFile(p)
k32.CloseHandle(h)
io.write("done")
