using System;
using System.IO;

namespace F2X.FileSize.Diagnostic
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine("DIAGNÓSTICO DE LECTURA DE TAMAÑO DE ARCHIVO");
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine();

            if (args.Length == 0)
            {
                Console.WriteLine("Uso: dotnet run <ruta_al_archivo>");
                Console.WriteLine("Ejemplo: dotnet run \"C:\\Archivos\\Squirrel-Mono.exe\"");
                return;
            }

            string filePath = args[0];

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"❌ Archivo no encontrado: {filePath}");
                return;
            }

            var fileInfo = new FileInfo(filePath);
            long bytes = fileInfo.Length;
            double mb = (double)bytes / 1048576.0;

            Console.WriteLine($"Archivo: {fileInfo.Name}");
            Console.WriteLine($"Ruta completa: {filePath}");
            Console.WriteLine();
            Console.WriteLine($"Bytes (FileInfo.Length): {bytes:N0}");
            Console.WriteLine($"MB exacto: {mb:F15}");
            Console.WriteLine();

            // Método de formateo actual
            string formatted = FormatFileSizeWindows(bytes);
            Console.WriteLine($"Formateado (nuestro código): {formatted}");
            Console.WriteLine();

            // Desglose del cálculo
            if (bytes >= 1048576 && bytes < 1073741824 && mb < 10)
            {
                long mbInt = (long)Math.Truncate(mb);
                double decimalPart = mb - mbInt;
                double multiplied = decimalPart * 100.0;
                long mbDec = (long)Math.Truncate(multiplied);

                Console.WriteLine("Desglose del cálculo:");
                Console.WriteLine($"  Parte entera: {mbInt}");
                Console.WriteLine($"  Parte decimal: {decimalPart:F15}");
                Console.WriteLine($"  Decimal * 100: {multiplied:F15}");
                Console.WriteLine($"  Truncate(decimal * 100): {mbDec}");
                Console.WriteLine($"  Resultado final: {mbInt},{mbDec:D2} MB");
            }

            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine("PRESIONA ENTER PARA SALIR");
            Console.ReadLine();
        }

        static string FormatFileSizeWindows(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} Bytes";
            }
            else if (bytes < 1048576)
            {
                double kb = (double)bytes / 1024.0;

                if (kb < 10)
                {
                    long kbInt = (long)Math.Truncate(kb);
                    long kbDec = (long)Math.Truncate((kb - kbInt) * 100.0);
                    return $"{kbInt},{kbDec:D2} KB";
                }
                else if (kb < 100)
                {
                    long kbInt = (long)Math.Truncate(kb);
                    long kbDec = (long)Math.Truncate((kb - kbInt) * 10.0);
                    return $"{kbInt},{kbDec} KB";
                }
                else
                {
                    long kbTruncated = (long)Math.Truncate(kb);
                    return $"{kbTruncated} KB";
                }
            }
            else if (bytes < 1073741824)
            {
                double mb = (double)bytes / 1048576.0;

                if (mb >= 100)
                {
                    long mbTruncated = (long)Math.Truncate(mb);
                    return $"{mbTruncated} MB";
                }
                else if (mb < 10)
                {
                    long mbInt = (long)Math.Truncate(mb);
                    long mbDec = (long)Math.Truncate((mb - mbInt) * 100.0);
                    return $"{mbInt},{mbDec:D2} MB";
                }
                else
                {
                    long mbInt = (long)Math.Truncate(mb);
                    long mbDec = (long)Math.Truncate((mb - mbInt) * 10.0);

                    if (mbDec == 0)
                    {
                        return $"{mbInt} MB";
                    }
                    else
                    {
                        return $"{mbInt},{mbDec} MB";
                    }
                }
            }
            else
            {
                double gb = (double)bytes / 1073741824.0;

                if (gb >= 100)
                {
                    long gbTruncated = (long)Math.Truncate(gb);
                    return $"{gbTruncated} GB";
                }
                else if (gb < 10)
                {
                    long gbInt = (long)Math.Truncate(gb);
                    long gbDec = (long)Math.Truncate((gb - gbInt) * 100.0);
                    return $"{gbInt},{gbDec:D2} GB";
                }
                else
                {
                    long gbInt = (long)Math.Truncate(gb);
                    long gbDec = (long)Math.Truncate((gb - gbInt) * 10.0);

                    if (gbDec == 0)
                    {
                        return $"{gbInt} GB";
                    }
                    else
                    {
                        return $"{gbInt},{gbDec} GB";
                    }
                }
            }
        }
    }
}