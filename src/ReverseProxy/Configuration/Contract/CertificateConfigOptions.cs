// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Configuration.Contract
{
    /// <summary>
    /// SSL certificate configuration.
    /// </summary>
    public class CertificateConfigOptions
    {
        public string Path { get; set; }

        public string KeyPath { get; set; }

        public string Password { get; set; }

        public string Subject { get; set; }

        public string Store { get; set; }

        public string Location { get; set; }

        public bool? AllowInvalid { get; set; }

        internal bool IsFileCert => !string.IsNullOrEmpty(Path);

        internal bool IsStoreCert => !string.IsNullOrEmpty(Subject);

        internal CertificateConfigOptions DeepClone()
        {
            return new CertificateConfigOptions
            {
                Path = Path,
                KeyPath = KeyPath,
                Password = Password,
                Subject = Subject,
                Store = Store,
                Location = Location,
                AllowInvalid = AllowInvalid
            };
        }
    }
}
