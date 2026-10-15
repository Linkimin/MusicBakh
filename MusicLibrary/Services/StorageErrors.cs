using System.Data.Common;
using System.IO;
using Microsoft.EntityFrameworkCore;

namespace MusicLibrary.Services;

internal static class StorageErrors
{
    public static bool IsExpected(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or DbException or DbUpdateException;
}
