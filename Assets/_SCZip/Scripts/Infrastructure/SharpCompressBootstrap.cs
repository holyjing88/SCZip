namespace SCZip.Infrastructure
{
    internal static class SharpCompressBootstrap
    {
        private static bool _initialized;

        public static void EnsureInitialized()
        {
            if (_initialized)
                return;

            System.Text.Encoding.RegisterProvider(
                System.Text.CodePagesEncodingProvider.Instance);
            _initialized = true;
        }
    }
}
