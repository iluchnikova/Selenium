using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support;
//using Newtonsoft.Json;
using System.IO;
using System.Threading;
using System.Data;
using System.Data.SqlClient;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;

namespace Selenium
{
    public static class MyMonitor
    {
        private static Thread thread;

        static MyMonitor()
        {
            thread = Thread.CurrentThread;
        }

        public static void Lock(string filePath, string readOrWrite)
        {
            //если файл заблокирован
            if (IsFileLocker(filePath))
            {
                //ожидаем его разблокировки
                while (IsFileLocker(filePath))
                {
                    //при разблокировке создается файл lock
                }
            }
            else
            {
                IsFileLocker(filePath);
            }

            Console.WriteLine("Поток {0} осуществляет {1} файла {2}", Thread.CurrentThread.Name, readOrWrite, new FileInfo(filePath).Name);
        }

        public static void Unlock(string filePath)
        {
            //задаем максимальный приоритет потоку, удаляющему lock-файл
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            string lockFile = @"" + new FileInfo(filePath).DirectoryName + "/lock_" + new FileInfo(filePath).Name.Substring(0, 2) + ".txt";

            //Если файл заблокирован
            if (IsFileLocker(filePath))
            {
                //Пытаемся удалить lock-файл
                while (DeleteLockFile(lockFile))
                {

                }
            }

            //возвращаем нормальный приоритет
            Thread.CurrentThread.Priority = ThreadPriority.Normal;
        }

        //Метод, определяющий заблокирован ли файл filePath (есть ли файл lock_+"filePath.Name")
        private static bool IsFileLocker(string filePath)
        {
            //определяем имя файла, который будет являться флагом блокировки
            string lockFile = @"" + new FileInfo(filePath).DirectoryName + "/lock_" + new FileInfo(filePath).Name.Substring(0, 2) + ".txt";

            //пробуем создать файл. Если файл уже создан вернется значение true, то есть файл уже используется другим потоком
            try
            {
                File.Create(lockFile);
            }
            catch
            {
                Thread.Yield();
                return true;
            }

            return false;
        }

        private static bool DeleteLockFile(string lockFilePath)
        {
            try
            {
                File.Delete(lockFilePath);
            }
            catch
            {
                return false;
            }
            return true;
        }

        /*//вызов открытия файла через консоль
        public static void Waiting(string filePath, string readOrWrite)
        {
            if (IsFileLocker(filePath))
            {
                while (IsFileLocker(filePath))
                { }
            }

            FileStream file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            lock (file)
            {
                file.Close();
                string fileName = new FileInfo(filePath).Name;
                Console.WriteLine("Поток {0} осуществляет {1} файла {2}", Thread.CurrentThread.Name, readOrWrite, fileName);
            }
        }

        //Метод, определяющий используется ли в настоящее время файл
        private static bool IsFileLocker(string filePath)
        {
            try
            {
                //проверяем возможность открыть для чтения
                FileStream fileOpen = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                fileOpen.Close();
            }
            catch
            {
                return true;
            }
            return false;
        }*/
    }

    [DataContract]
    class TextAndId
    {
        [DataMember]
        public string ID { get; set; }
        [DataMember]
        public string Text { get; set; }
    }

    [DataContract]
    class ImageAndId
    {
        [DataMember]
        public string ID { get; set; }
        [DataMember]
        public string[] Image { get; set; }
    }

    [DataContract]
    class UrlAndId
    {
        [DataMember]
        public string ID { get; set; }
        [DataMember]
        public string[] Url { get; set; }
    }

    class PostFromVK
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public string[] Image { get; set; }
        public string[] Url { get; set; }

        public PostFromVK(string id, string text, string[] image, string[] url)
        {
            this.Id = id;
            this.Text = Screen(text);
            this.Image = image;
            this.Url = url;
        }

        //Экранирование специальных символов json для десериализации файла
        public static string Screen(string text)
        {
            if (text != null)
            {
                StringBuilder json = new StringBuilder(text.Length);

                foreach (var t in text)
                {
                    switch (t)
                    {
                        case '\b':
                            json.Append(@"\b");
                            break;
                        case '\f':
                            json.Append(@"\f");
                            break;
                        case '\n':
                            json.Append(@"\n");
                            break;
                        case '\r':
                            json.Append(@"\r");
                            break;
                        case '\t':
                            json.Append(@"\t");
                            break;
                        case '\'':
                            json.Append(@"\'");
                            break;
                        case '\"':
                            json.Append("\\\"");
                            break;
                        case '\\':
                            json.Append(@"\\");
                            break;
                        default:
                            json.Append(t);
                            break;
                    }
                }
                return json.ToString();
            }
            else
                return null;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            //поток T0
            Thread t0 = new Thread(() => GetNews());//new ThreadStart(GetNews));
            t0.Start();
        }

        public static void GetNews()//парсинг новостей ВК
        {
            //Удаляем lock-файлы при запуске
            DeleteLockFiles();

            //открытие окна браузера Google Chrome
            ChromeDriver chrDriver = new ChromeDriver();

            //открываем страницу новостей ВК
            chrDriver.Navigate().GoToUrl("https://vk.com/feed");

            //Ожидание загрузки страницы в 10 секунд
            chrDriver.Manage().Timeouts().SetPageLoadTimeout(TimeSpan.FromSeconds(10));

            //Авторизация
            Autorisation(chrDriver);

            Thread t4 = new Thread(() => ReadFile())//new ThreadStart(ReadFile))
            {
                Name = "t4"
            };
            t4.Start();

            while (true)
            {
                string id;
                string text;
                string[] image;
                string[] url;

                //Создаем список для определения количества постов, видимых на странице
                List<IWebElement> Posts = chrDriver.FindElements(By.XPath(".//div[contains(@class, '_post post page_block') and @id]")).ToList();

                //Cоздаем список id
                List<string> IdOfPost = new List<string>();
                //Заполняем список значениями атрибута data-post-id в каждом веб-элементе списка Posts
                foreach (IWebElement post in Posts)
                {
                    id = post.GetAttribute("data-post-id");
                    IdOfPost.Add(id);
                }

                foreach (var i in IdOfPost)
                {
                    // Получаем текст новости 
                    if (IsElementExists(By.XPath(".//div[@id='post" + i + "']//div[@class='wall_post_text']"), chrDriver))
                        text = chrDriver.FindElement(By.XPath(".//div[@id='post" + i + "']//div[@class='wall_post_text']")).Text;
                    else
                        text = "no_text";

                    // Получаем изображение(я) новости 
                    if (IsElementExists(By.XPath(".//div[@id='post" + i + "']//div[contains(@class,'page_post_sized_thumbs')]"), chrDriver))
                    {
                        List<IWebElement> Images = chrDriver.FindElements(By.XPath(".//div[@id='post" + i + "']//div[contains(@class,'page_post_sized_thumbs')]/a")).ToList();
                        List<string> ListOfImage = new List<string>();
                        foreach (IWebElement im in Images)
                        {
                            ListOfImage.Add(LinkFromText(im.GetAttribute("onclick")));
                        }
                        image = ListOfImage.ToArray();
                    }
                    else
                    {
                        image = new string[1];//пустой массив
                        image[0] = "no_image";
                    }

                    // Получаем ссылки новости 
                    List<IWebElement> Urls = chrDriver.FindElements(By.XPath(".//div[@id='post" + i + "']//a")).ToList();
                    List<string> ListOfUrl = new List<string>();
                    foreach (IWebElement u in Urls)
                    {
                        ListOfUrl.Add(u.GetAttribute("href"));
                    }
                    url = ListOfUrl.ToArray();

                    // Добавляем новый элемент в списко news
                    PostFromVK post = new PostFromVK(i, text, image, url);

                    Thread t1 = new Thread(() => RecordText(post))//new ParameterizedThreadStart(RecordText))
                    {
                        Name = "t1"
                    };
                    t1.Start();
                    Thread t2 = new Thread(() => RecordImage(post))//new ParameterizedThreadStart(RecordImage))
                    {
                        Name = "t2"
                    };
                    t2.Start();
                    Thread t3 = new Thread(() => RecordUrl(post))//new ParameterizedThreadStart(RecordUrl))
                    {
                        Name = "t3"
                    };
                    t3.Start();

                    t1.Join();
                    t2.Join();
                    t3.Join();
                }

                //Прокрутка страницы
                IJavaScriptExecutor js = (IJavaScriptExecutor)chrDriver;
                js.ExecuteScript("window.scrollTo(0, document.body.scrollHeight)");
            }
        }

        public static void RecordText(object obj)
        {
            //Приводим входящее значение к типу PostFromVK
            PostFromVK post = (PostFromVK)obj;

            //Задаем в переменную путь, где будут храниться записанные данные
            string filePath = @"C:\Users\Samsung\source\repos\Selenium\Selenium\f1.json";

            //Создаем новую переменную на основе данных из переменной post
            TextAndId textAndId = new TextAndId { ID = post.Id, Text = post.Text };

            DataContractJsonSerializer jsonFormatter = new DataContractJsonSerializer(typeof(TextAndId[]));

            //Если файл существует
            if (File.Exists(filePath))
            {
                //включаем монитор, т.к. нужно прочитать файл
                MyMonitor.Lock(filePath, "запись");

                List<TextAndId> toFile = null;//новый список для сериализации и записи в файл

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    //десериализируем данные из файла в список
                    TextAndId[] fromFile = (TextAndId[])jsonFormatter.ReadObject(fs);

                    int n = 0;
                    for (int i = 0; i < fromFile.Length; i++)
                    {
                        if (fromFile[i].ID.Equals(post.Id))
                            n++;
                    }
                    //Если файл не пустой и текст не содержит id входящего объекта и этот id не нулевой
                    if (fromFile.Length > 0 && n == 0 && !post.Id.Equals(""))
                    {
                        toFile = fromFile.ToList();
                        toFile.Add(textAndId);
                    }
                    fs.Dispose();
                }

                if (toFile != null)
                {
                    //сериализируем полученный список toFile и записываем его в файл (FileMode.Truncate очищает файл)
                    using (FileStream fs = new FileStream(filePath, FileMode.Truncate, FileAccess.ReadWrite))
                    {
                        jsonFormatter.WriteObject(fs, toFile.ToArray());
                        fs.Dispose();
                    }
                }

                MyMonitor.Unlock(filePath);
            }
            else
            {
                using (FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate))
                {
                    List<TextAndId> newTextAndId = new List<TextAndId>();
                    newTextAndId.Add(textAndId);
                    jsonFormatter.WriteObject(fs, newTextAndId.ToArray());
                }
                string fileName = new FileInfo(filePath).Name;
                Console.WriteLine("Поток {0} создал новый файл {1}", Thread.CurrentThread.Name, fileName);
            }
        }

        public static void RecordImage(object obj)
        {
            //Приводим входящее значение к типу PostFromVK
            PostFromVK post = (PostFromVK)obj;

            //Задаем в переменную путь, где будут храниться записанные данные
            string filePath = @"C:\Users\Samsung\source\repos\Selenium\Selenium\f2.json";

            //Создаем новую переменную на основе данных из переменной post
            ImageAndId imageAndId = new ImageAndId { ID = post.Id, Image = post.Image };

            DataContractJsonSerializer jsonFormatter = new DataContractJsonSerializer(typeof(ImageAndId[]));

            //Если файл существует
            if (File.Exists(filePath))
            {
                //включаем монитор, т.к. нужно прочитать файл
                MyMonitor.Lock(filePath, "запись");

                List<ImageAndId> toFile = null;//новый список для сериализации и записи в файл

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    //десериализируем данные из файла в список
                    ImageAndId[] fromFile = (ImageAndId[])jsonFormatter.ReadObject(fs);

                    int n = 0;
                    for (int i = 0; i < fromFile.Length; i++)
                    {
                        if (fromFile[i].ID.Equals(post.Id))
                            n++;
                    }
                    //Если файл не пустой и текст не содержит id входящего объекта и этот id не нулевой
                    if (fromFile.Length > 0 && n == 0 && !post.Id.Equals(""))
                    {
                        toFile = fromFile.ToList();
                        toFile.Add(imageAndId);
                    }
                    fs.Dispose();
                }

                if (toFile != null)
                {
                    //сериализируем полученный список toFile и записываем его в файл (FileMode.Truncate очищает файл)
                    using (FileStream fs = new FileStream(filePath, FileMode.Truncate, FileAccess.ReadWrite))
                    {
                        jsonFormatter.WriteObject(fs, toFile.ToArray());
                        fs.Dispose();
                    }
                }

                MyMonitor.Unlock(filePath);
            }
            else
            {
                using (FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate))
                {
                    List<ImageAndId> newImageAndId = new List<ImageAndId>();
                    newImageAndId.Add(imageAndId);
                    jsonFormatter.WriteObject(fs, newImageAndId.ToArray());
                }
                string fileName = new FileInfo(filePath).Name;
                Console.WriteLine("Поток {0} создал новый файл {1}", Thread.CurrentThread.Name, fileName);
            }
        }

        public static void RecordUrl(object obj)
        {
            //Приводим входящее значение к типу PostFromVK
            PostFromVK post = (PostFromVK)obj;

            //Задаем в переменную путь, где будут храниться записанные данные
            string filePath = @"C:\Users\Samsung\source\repos\Selenium\Selenium\f3.json";

            //Создаем новую переменную на основе данных из переменной post
            UrlAndId urlAndId = new UrlAndId { ID = post.Id, Url = post.Url };

            DataContractJsonSerializer jsonFormatter = new DataContractJsonSerializer(typeof(UrlAndId[]));

            //Если файл существует
            if (File.Exists(filePath))
            {
                //включаем монитор, т.к. нужно прочитать файл
                MyMonitor.Lock(filePath, "запись");

                List<UrlAndId> toFile = null;//новый список для сериализации и записи в файл

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    //десериализируем данные из файла в список
                    UrlAndId[] fromFile = (UrlAndId[])jsonFormatter.ReadObject(fs);

                    int n = 0;
                    for (int i = 0; i < fromFile.Length; i++)
                    {
                        if (fromFile[i].ID.Equals(post.Id))
                            n++;
                    }
                    //Если файл не пустой и текст не содержит id входящего объекта и этот id не нулевой
                    if (fromFile.Length > 0 && n == 0 && !post.Id.Equals(""))
                    {
                        toFile = fromFile.ToList();
                        toFile.Add(urlAndId);
                    }
                    fs.Dispose();
                }

                if (toFile != null)
                {
                    //сериализируем полученный список toFile и записываем его в файл (FileMode.Truncate очищает файл)
                    using (FileStream fs = new FileStream(filePath, FileMode.Truncate, FileAccess.ReadWrite))
                    {
                        jsonFormatter.WriteObject(fs, toFile.ToArray());
                        fs.Dispose();
                    }
                }

                MyMonitor.Unlock(filePath);
            }
            else
            {
                using (FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate))
                {
                    List<UrlAndId> newUrlAndId = new List<UrlAndId>();
                    newUrlAndId.Add(urlAndId);
                    jsonFormatter.WriteObject(fs, newUrlAndId.ToArray());
                }
                string fileName = new FileInfo(filePath).Name;
                Console.WriteLine("Поток {0} создал новый файл {1}", Thread.CurrentThread.Name, fileName);
            }
        }

        public static void ReadFile()
        {
            while (true)
            {
                //Получаем список фойлов по указанной директории
                DirectoryInfo directory = new DirectoryInfo(@"C:\Users\Samsung\source\repos\Selenium\Selenium");
                List<string> files = directory.GetFiles("*.json").Select(f => f.Name).ToList();

                if (files.Count > 0)
                {
                    //Выбираем файл с рандомным индексом из списка files
                    Random random = new Random();
                    int index = random.Next(0, files.Count - 1);

                    //указываем директорию
                    string filePath = @"C:\Users\Samsung\source\repos\Selenium\Selenium\" + files[index];

                    //Включаем монитор
                    MyMonitor.Lock(filePath, "чтение");
                    using (StreamReader sr = new StreamReader(filePath))
                    {
                        sr.Close();
                    }
                    //Выключаем монитор
                    MyMonitor.Unlock(filePath);
                    Thread.Sleep(1000);
                }
            }
        }

        //Метод, выделяющий из текста ссылки
        public static string LinkFromText(string text)
        {
            string link = null;
            //if (text.Contains("https://") && text != null || text.Contains("http://") && text != null)
            //{
            //Разбиваем текст на участки. Раззделителем служатдвойные кавычки
            string[] partsOfText = text.Split('\"');

            //Выбираем из полученного массива участок, содержащий "https://" или "http://"
            for (int i = 0; i < partsOfText.Length; i++)
            {
                if (partsOfText[i].Contains("https://") || partsOfText[i].Contains("http://"))
                    link = partsOfText[i];
            }
            //}

            return link;
        }

        //Проверка на существование элемента на веб-странице
        public static bool IsElementExists(By iClassName, ChromeDriver chrDriver)
        {
            try
            {
                chrDriver.FindElement(iClassName);
            }
            catch
            {
                return false;
            }
            return true;
        }

        // Удаляем файлы lock
        static void DeleteLockFiles()
        {
            //Получаем список фойлов по указанной директории
            DirectoryInfo directory = new DirectoryInfo(@"C:\Users\Samsung\source\repos\Selenium\Selenium");
            List<string> lockFiles = directory.GetFiles("lock*.txt").Select(f => f.Name).ToList();
            foreach (var l in lockFiles)
            {
                File.Delete(@"C:\Users\Samsung\source\repos\Selenium\Selenium\" + l);
            }
        }

        //Авторизация ВК
        public static void Autorisation(ChromeDriver chrDriver)
        {
            string login = "89160394648";
            string password;

            //Подключаемся к БД с Логином и паролем
            SqlConnection connection = new SqlConnection(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\Samsung\source\repos\Selenium\Selenium\Login.mdf;Integrated Security=True");
            connection.Open();

            //создаем таблицу из подходящих под параметры данных
            SqlDataAdapter sda = new SqlDataAdapter("SELECT * FROM Login WHERE Login = '" + login + "'", connection);
            DataTable dt = new DataTable();
            sda.Fill(dt);

            //Присваиваем паролю соответствующее значение из таблицы
            password = dt.Rows[0].ItemArray[1].ToString();

            //Осуществляем действия на странице
            IWebElement loginOnThePage = chrDriver.FindElement(By.Id("email"));
            loginOnThePage.SendKeys(login + Keys.Tab + password + Keys.Enter);

            //Отсоединяемся от БД
            if (connection != null)
                connection.Close();
        }
    }
}
