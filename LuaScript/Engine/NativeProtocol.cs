namespace LuaScript.Engine
{
    internal static class NativeProtocol
    {
        public const int Magic = 0x4C4A4954;

        public const int CmdRun = 0;
        public const int CmdShutdown = 1;

        public const int StatusIdle = 0;
        public const int StatusOk = 1;
        public const int StatusError = 2;
        public const int StatusCallback = 3;

        public const int OffMagic = 0;
        public const int OffCommand = 4;
        public const int OffStatus = 8;
        public const int OffWidth = 12;
        public const int OffHeight = 16;
        public const int OffScriptLen = 20;
        public const int OffPixelsDirty = 24;
        public const int OffErrorLen = 28;
        public const int OffCallbackFrame = 32;
        public const int OffCallbackTagLen = 36;
        public const int OffCallbackFound = 40;
        public const int OffCallbackKind = 44;
        public const int OffLoadResultWidth = 48;
        public const int OffLoadResultHeight = 52;
        public const int OffStringParamsLen = 56;
        public const int OffScriptVersion = 60;

        public const int HeaderSize = 64;

        public const int FieldCount = 64;
        public const int FieldsOffset = HeaderSize;

        public const int ScriptOffset = FieldsOffset + FieldCount * 8;
        public const int ScriptMax = 128 * 1024;

        public const int ErrorOffset = ScriptOffset + ScriptMax;
        public const int ErrorMax = 4 * 1024;

        public const int CallbackTagOffset = ErrorOffset + ErrorMax;
        public const int CallbackTagMax = 4096;
        public const int CallbackResultOffset = CallbackTagOffset + CallbackTagMax;
        public const int CallbackResultCount = 8;

        public const int StringParamsOffset = CallbackResultOffset + CallbackResultCount * 8;
        public const int MinStringParamsCapacity = 64 * 1024;

        public const int CbKindGetObject = 0;
        public const int CbKindLoadFigure = 1;
        public const int CbKindEffect = 2;
        public const int CbKindDraw = 3;
        public const int CbKindDrawPoly = 4;
        public const int CbKindLoadText = 5;
        public const int CbKindLoadImage = 6;
        public const int CbKindLoadMovie = 7;
        public const int CbKindSetAnchor = 8;
        public const int CbKindRequestPixels = 9;
        public const int CbKindFlushDraws = 10;

        public const int DrawRingCapacity = 4096;
        public const int DrawEntryDoubles = 24;
        public const int DrawRingDoubles = 1 + DrawRingCapacity * DrawEntryDoubles;
        public const long DrawRingBytes = (long)DrawRingDoubles * 8;

        public const int CbExist = 0;
        public const int CbX = 1;
        public const int CbY = 2;
        public const int CbZ = 3;
        public const int CbZoom = 4;
        public const int CbRz = 5;
        public const int CbAlpha = 6;
        public const int CbLayer = 7;

        public static long DrawRingOffset(int stringCapacity) => StringParamsOffset + stringCapacity;

        public static long PixelOffset(int stringCapacity) => DrawRingOffset(stringCapacity) + DrawRingBytes;

        public const int W = 0;
        public const int H = 1;
        public const int Hw = 2;
        public const int Hh = 3;
        public const int Cx = 4;
        public const int Cy = 5;
        public const int Cz = 6;
        public const int Diagonal = 7;
        public const int X = 8;
        public const int Y = 9;
        public const int Z = 10;
        public const int Ox = 11;
        public const int Oy = 12;
        public const int Oz = 13;
        public const int Sx = 14;
        public const int Sy = 15;
        public const int Sz = 16;
        public const int Zoom = 17;
        public const int Aspect = 18;
        public const int Alpha = 19;
        public const int Rx = 20;
        public const int Ry = 21;
        public const int Rz = 22;
        public const int Rxr = 23;
        public const int Ryr = 24;
        public const int Rzr = 25;
        public const int Track0 = 26;
        public const int Track1 = 27;
        public const int Track2 = 28;
        public const int Track3 = 29;
        public const int Time = 30;
        public const int Frame = 31;
        public const int TotalFrame = 32;
        public const int TotalTime = 33;
        public const int T = 34;
        public const int Framerate = 35;
        public const int Layer = 36;
        public const int Index = 37;
        public const int Num = 38;
        public const int SceneWidth = 39;
        public const int SceneHeight = 40;
        public const int SceneCx = 41;
        public const int SceneCy = 42;
        public const int GroupIndex = 43;
        public const int GroupCount = 44;
        public const int TimelineTotalFrame = 45;
        public const int TimelineTotalTime = 46;
        public const int TimeRatio = 47;
        public const int IsSaving = 48;
        public const int IsPlaying = 49;
        public const int IsPaused = 50;
        public const int TimelineFrame = 51;
        public const int TimelineTime = 52;
        public const int Check0 = 53;
        public const int Check1 = 54;
        public const int Check2 = 55;
        public const int Check3 = 56;
        public const int Color = 57;
        public const int Slider0 = 58;
        public const int Slider1 = 59;
        public const int Slider2 = 60;
        public const int Slider3 = 61;
        public const int DrawState = 62;

        public const int FirstWritableField = X;
        public const int LastWritableField = Rzr;

        public const int MaxPixelBufferSize = 3840 * 2160 * 4;

        public static long BufferSize(int width, int height, int stringCapacity) =>
            PixelOffset(stringCapacity) + Math.Max((long)width * height * 4, MaxPixelBufferSize);
    }
}
