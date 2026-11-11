namespace OpenDAoC.Pathing
{
    // Owns a loaded native dtNavMesh.
    public sealed class DetourNavMesh : IDisposable
    {
        private IntPtr _mesh;
        private bool _disposed;

        private DetourNavMesh(IntPtr mesh)
        {
            _mesh = mesh;
        }

        public IntPtr Handle
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _mesh;
            }
        }

        public static bool TryProbeNativeLibrary(out Exception? error)
        {
            if (!DetourNativeLibrary.TryLoad(out string? loadedPath, out Exception? loadError))
            {
                error = loadError ?? new DllNotFoundException("Unable to load native Detour library.");
                return false;
            }

            try
            {
                // Exercise a P/Invoke entry point so LibraryImport bindings are fully resolved.
                IntPtr dummy = IntPtr.Zero;
                DetourNative.LoadNavMeshWithTileCount("this file does not exist!", ref dummy, out _, out _);
                error = null;
                _ = loadedPath;
                return true;
            }
            catch (Exception e)
            {
                error = e;
                return false;
            }
        }

        public static DetourNavMesh? TryLoad(string path)
        {
            return TryLoad(path, out _, out _);
        }

        // Fails if no tile could be added. Fewer tiles loaded than in the file means some were rejected
        // (e.g. a file saved with 32-bit tile refs, while this library uses 64-bit refs).
        public static DetourNavMesh? TryLoad(string path, out int tilesInFile, out int tilesLoaded)
        {
            tilesInFile = 0;
            tilesLoaded = 0;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            IntPtr meshPtr = IntPtr.Zero;

            if (!DetourNative.LoadNavMeshWithTileCount(Path.GetFullPath(path), ref meshPtr, out tilesInFile, out tilesLoaded) || meshPtr == IntPtr.Zero)
                return null;

            return new(meshPtr);
        }

        public static DetourNavMesh Load(string path)
        {
            DetourNavMesh mesh = TryLoad(path) ?? throw new InvalidOperationException($"Failed to load navmesh from '{path}'.");
            return mesh;
        }

        public DetourNavMeshQuery CreateQuery()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return DetourNavMeshQuery.Create(this);
        }

        public EDtStatus UpdateFlags(ReadOnlySpan<ulong> polyRefs, EDtPolyFlags flagsToRemove, EDtPolyFlags flagsToAdd)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return polyRefs.IsEmpty ? EDtStatus.DT_SUCCESS : DetourNative.UpdateFlags(_mesh, polyRefs, polyRefs.Length, flagsToRemove, flagsToAdd);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_mesh != IntPtr.Zero)
            {
                DetourNative.FreeNavMesh(_mesh);
                _mesh = IntPtr.Zero;
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
