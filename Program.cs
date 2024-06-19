class Program
{
    private static string sourceDirectory;
    private static string resultDirectory;
    private static FileSystemWatcher watcher;
    private static SemaphoreSlim semaphore = new SemaphoreSlim(4); // 4 рабочих потока
    private static CancellationTokenSource cts = new CancellationTokenSource();

    static async Task Main(string[] args)
    {

        if (args.Length != 2)
        {
            Console.WriteLine("Usage: Program <sourceDirectory> <resultDirectory>");
            return;
        }

        // Проверка существования директорий
        if (!Directory.Exists(sourceDirectory) || !Directory.Exists(resultDirectory))
        {
            Console.WriteLine("One or both of the directories do not exist.");
            return;
        }

        // Обработка уже существующих файлов в папке
        var existingFiles = Directory.GetFiles(sourceDirectory, "*.txt");
        var tasks = existingFiles.Select(ProcessFile);
        await Task.WhenAll(tasks);

        // Настройка FileSystemWatcher для отслеживания новых файлов
        watcher = new FileSystemWatcher(sourceDirectory, "*.txt")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
        };
        watcher.Created += OnCreated;
        watcher.EnableRaisingEvents = true;

        Console.WriteLine("Press 'q' to quit the application.");
        while (Console.Read() != 'q') ;

        // Завершение работы
        cts.Cancel();
        watcher.EnableRaisingEvents = false;
        await Task.WhenAll(tasks);
        semaphore.Dispose();
    }

    private static async void OnCreated(object sender, FileSystemEventArgs e)
    {
        try
        {
            await semaphore.WaitAsync(cts.Token);
            await ProcessFile(e.FullPath);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Operation was cancelled.");
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static async Task ProcessFile(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        string resultFilePath = Path.Combine(resultDirectory, fileName);

        try
        {
            // Чтение файла
            string content = await File.ReadAllTextAsync(filePath);
            int letterCount = content.Count(char.IsLetter);

            // Запись результата в новый файл
            await File.WriteAllTextAsync(resultFilePath, letterCount.ToString());
            Console.WriteLine($"Processed {fileName}: {letterCount} letters.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing {fileName}: {ex.Message}");
        }
    }
}