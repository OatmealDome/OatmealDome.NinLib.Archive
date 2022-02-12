# OatmealDome.NinLib.Archive

This library allows you to access individual files within a SARC archive. Yaz0 and Zstandard decompression is also supported.

Creating new SARC archives is not supported at this time.

## Usage

```csharp
byte[] data = File.ReadAllBytes("File.sarc");
Sarc sarc = new Sarc(data);

// Access an individual file
byte[] innerData = sarc["InnerFile.bin"];

// Iterate over every file in the archive
foreach (KeyValuePair<string, byte[]> file in sarc)
{
    // do something with file.Key and file.Value...
}
```
