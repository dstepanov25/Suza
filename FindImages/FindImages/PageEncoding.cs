using System;
using System.Net;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

public class PageEncoding
{
    /* Глобальные переменные */
    static int len_;
    static byte[] text_;
    //static FILE* f_;

    /* Таблица сочетаний */
    static byte[] table_2s ={0xFF,0xFF,0xFF,0xC7,0xFE,0xBE,0xF7,0xFB,
                                0xFD,0xBF,0xF7,0xF9,0xFC,0xBE,0xF1,0x80,0xFF,0xFF,0xF7,0xBB,0xFF,0xFF,0xFF,
                                0xCF,0xDE,0xBF,0xD1,0x08,0xFF,0xBF,0xF1,0xBF,0xFF,0xFF,0xFF,0xC7,0x1D,0x3F,
                                0x7F,0x81,0xA7,0xB6,0xF2,0x82,0xFF,0xFF,0x75,0xDB,0xFC,0xBF,0xD7,0x9D,0xFF,
                                0xAE,0xFB,0xDF,0xFF,0xFF,0xFF,0xC7,0x84,0xB7,0xF3,0x9F,0xFF,0xFF,0xFF,0xDB,
                                0xFF,0xBF,0xFF,0xFF,0xFD,0xBF,0xFF,0xFF,0xFF,0xFF,0xE7,0xC7,0x84,0x9E,0xF0,
                                0x12,0xBC,0xBF,0xF0,0x84,0xA4,0xBA,0x10,0x10,0xA4,0xBE,0xB8,0x88,0xAC,0xBF,
                                0xF7,0x0A,0x84,0x86,0x90,0x08,0x04,0x00,0x00,0x03,0x7F,0xFD,0xF7,0xC1,0x7D,
                                0xAE,0x6F,0xCB,0x15,0x3D,0xFC,0x00,0x7F,0x7D,0xE7,0xC2,0x7F,0xFD,0xF7,0xC3};

    /// <summary>
    /// Вспомогательная функция alt2num
    /// </summary>
    /// <param name="a">код русской буквы в кодировке ALT</param>
    /// <returns>порядковый номер этой буквы (0-31)</returns>
    static int alt2num(int a)
    {
        if (a >= 0xE0) a -= 0x30;
        return (a & 31);
    }

    /// <summary>
    /// Вспомогательная функция koi2num
    /// </summary>
    /// <param name="a">a - код русской буквы в кодировке KOI</param>
    /// <returns>порядковый номер этой буквы (0-31)</returns>
    static int koi2num(int a)
    {
        byte[] t ={30,0,1,22,4,5,20,3,21,8,9,10,11,12,13,14,15,31,
          16,17,18,19,6,2,28,27,7,24,29,25,23,26};

        return (t[a & 31]);
    }

    /// <summary>
    /// Вспомогательная функция work_2s - обработка двухбуквенного сочетания.
    /// </summary>
    /// <param name="c1">порядковый номер первой буквы (0-31)</param>
    /// <param name="c2">порядковый номер второй буквы (0-31)</param>
    /// <param name="check">надо ли проверять, встречалось ли сочетание раньше (1 - да, 0 - нет),</param>
    /// <param name="buf">адрес массива с информацией о встреченных сочетаниях</param>
    /// <returns>0 - указанное сочетание уже встречалось раньше,
    ///  1 - сочетание не встречалось раньше и является допустимым,
    ///  2 - сочетание не встречалось раньше и является недопустимым</returns>
    static int work_2s(int c1, int c2, int check, byte[] buf)
    {
        int i = (c1 << 2) + (c2 >> 3); /* Номер байта в массиве. */

        byte mask = (byte)(0x80 >> (c2 & 7)); /* Маска, соответствующая номеру бита в байте. */

        /* Если check=1, проверяем: если соответствующий бит массива buf равен 0,
           значит, указанное сочетание уже встречалось раньше. Тогда выходим из
           функции, возвращая 0. Если же сочетание не встречалось, то помечаем, что
           оно встретилось (обнуляем соответствующий бит массива buf). */

        if (check == 1)
        {
            if ((buf[i] & mask) == 0)
                return (0);
            buf[i] &= (byte)~mask;
        }

        /* Проверяем, допустимо сочетание или нет. */

        if ((table_2s[i] & mask) != 0)
            return (1); /* Допустимо. */
        return (2);                            /* Недопустимо. */
    }

    /* =========================================================================
       Вспомогательная функция def_code - определение кодировки текста. Функция
       m_def_code - лишь надстройка над этой функцией.
       Вход:  get_char - указатель на функцию, которую надо вызывать для получения
                         очередного символа текста. Функция должна возвращать либо
                         код символа, либо, при достижении конца текста, -1.
              n - количество различных сочетаний русских букв (1-255), которого
                  достаточно для определения кодировки.
       Выход: 0 - текст в кодировке ALT, 1 - WIN, 2 - KOI.
     ========================================================================= */

    public static Encoding def_code(byte[] text, int n)
    {
        /* Присваиваем значения глобальным переменным len_ и p_, которые будут
           доступны из функции m_get_char. */
        len_ = 0;
        text_ = text;
        /* Получаем результат. */
        /* В массиве buf_1 хранится информация о том, какие сочетания руских букв
           уже встречались в варианте ALT, а в массиве buf_2 - в варианте WIN. */

        byte[] buf_1 = new byte[128];
        byte[] buf_2 = new byte[128];

        int bad_1 = 0;
        int bad_2 = 0;
        int bad_3 = 0;
        int all_1 = 0;
        int all_3 = 0;  /* all_2=all_3 */

        int c1 = 0; /* Символы текущего обрабатываемого сочетания. */
        int c2 = 0;
        int i;

        /* Инициализация buf_1 и buf_2. */

        for (i = 0; i < 128; i++)
            buf_1[i] = 0xFF;
        for (i = 0; i < 128; i++)
            buf_2[i] = 0xFF;

        /* Главный цикл - обработка сочетаний для каждого из трёх вариантов. Цикл
           выполняется, пока не кончится текст или в каком-либо из вариантов не
           встретится n сочетаний. */
        while (((c2 = m_get_char()) != -1) && (all_1 < n) && (all_3 < n))
        {
            /* Вариант ALT. Вначале проверяем, являются ли символы текущего сочетания
               кодами русских букв в кодировке ALT. */

            if ((((c1 >= 0x80) && (c1 < 0xB0)) || ((c1 >= 0xE0) && (c1 < 0xF0))) &&
                (((c2 >= 0x80) && (c2 < 0xB0)) || ((c2 >= 0xE0) && (c2 < 0xF0))))
            {
                int result = work_2s(alt2num(c1), alt2num(c2), 1, buf_1);
                if (result == 2)
                {
                    bad_1++;
                    all_1++;
                }
                if (result == 1)
                    all_1++;
            }
            /* Варианты WIN и KOI. Вначале проверяем, являются ли символы текущего
               сочетания кодами русских букв в этих кодировках (в обеих кодировках
               диапазоны кодов русских букв совпадают). */

            if ((c1 & c2) >= 0xC0) /* Эквивалентно условию (c1>=0xC0)&&(c2>=0xC0). */
            {
                int result = work_2s(c1 & 31, c2 & 31, 1, buf_2);
                if (result == 0)/* Если сочетание букв уже встречалось в варианте WIN,
                                то оно уже встречалось и в варианте KOI, так что
                                пропускаем обработку варианта KOI и переходим
                                к следующей итерации главного цикла. */
                    continue;
                if (result == 2)
                    bad_2++;

                /* Если сочетание букв ещё не встречалось в варианте WIN, то оно заведомо
                   не встречалось и в варианте KOI, поэтому специально проверять это не
                   надо - значит, функцию work_2s вызываем с параметром check, равным 0. */
                result = work_2s(koi2num(c1), koi2num(c2), 0, null);
                if (result == 2)
                {
                    bad_3++;
                    all_3++;
                }
                if (result == 1)
                    all_3++;
            }
            c1 = c2;
        }

        /* Данные собраны. Теперь, если в каком-либо из вариантов недопустимых
           сочетаний не больше 1/32 от общего их числа, то считаем, что их и не
           было. */
        if (bad_1 <= (all_1 >> 5)) bad_1 = 0;
        if (bad_2 <= (all_3 >> 5)) bad_2 = 0;
        if (bad_3 <= (all_3 >> 5)) bad_3 = 0;

        /* Получаем результат. */

        {
            int a = ((255 - bad_1) << 8) + all_1;
            int b = ((255 - bad_2) << 8) + all_3;
            int c = ((255 - bad_3) << 8) + all_3;

            if ((a >= b) && (a >= c))
                return Encoding.GetEncoding(866);
            if (b >= c)
                return Encoding.GetEncoding(1251);
            else
                return Encoding.GetEncoding("koi8r");
        }
    }

    public static Encoding def_code(string text, int n)
    {
        byte[] b = new byte[text.Length];
        for (int i = 0; i < text.Length; i++)
            b[i] = (byte)text[i];
        return def_code(b, n);
    }
    /* =========================================================================
       Вспомогательная функция m_get_char вызывается из функции def_code, когда
       та вызвана из m_def_code.
       Выход: очередной символ текста или -1, если текст кончился.
     ========================================================================= */
    static int m_get_char()
    {
        if (len_ == text_.Length)
            return (-1);
        return text_[len_++];
    }

    public static string GetPage(string url, string userName, string password, string ip, int port)
    {
        WebResponse response = null;
        Stream stream = null;
        StreamReader reader = null;
        try
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            if (userName != null)
            {
                request.PreAuthenticate = true;
                request.Credentials = new NetworkCredential(userName, password);
            }
            request.Timeout = 30000;
            request.UserAgent = "MSIE 6.0";
            if (ip != "" && port != 0)
                request.Proxy = new WebProxy(ip, port);
            response = request.GetResponse();
            stream = response.GetResponseStream();
            var b = ReadFully(stream);
            Encoding enc = def_code(b, 100);
            string content = DecodeContent(b, enc);
            return content;
        }
        catch (WebException e)
        {
            //System.Console.WriteLine("Can't download:" + e);
            return null;
        }
        catch (Exception e)
        {
            //System.Console.WriteLine("Can't download:" + e);
            return null;
        }
        finally
        {
            if (reader != null)
                reader.Close();

            if (stream != null)
                stream.Close();

            if (response != null)
                response.Close();
        }
    }
    
    public static string GetUTFPage(string url, string ip, int port)
    {
        return GetUTFPage(url, "", ip, port);
    }

    public static string GetUTFPage(string url, string postData, string ip, int port)
    {
        WebResponse response = null;
        Stream stream = null;
        StreamReader reader = null;
        try
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "MSIE 6.0";
            if (ip != "" && port != 0)
                request.Proxy = new WebProxy(ip, port);
            if (postData != "")
            {
                ASCIIEncoding encoding = new ASCIIEncoding();
                byte[] data = encoding.GetBytes(postData);
                request.ContentLength = data.Length;
                request.ContentType = "application/x-www-form-urlencoded";
                request.Method = "POST";

                Stream newStream = request.GetRequestStream();
                // Send the data.
                newStream.Write(data, 0, data.Length);
                newStream.Close();
            }
            response = request.GetResponse();
            stream = response.GetResponseStream();
            reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            string buffer = reader.ReadToEnd();
            return buffer;
        }
        catch (WebException ex)
        {
            //System.Console.WriteLine("Can't download:" + e);
            if (postData != "" && ex.Response != null)
            {
                response = ex.Response as HttpWebResponse;
                stream = response.GetResponseStream();
                reader = new StreamReader(stream, System.Text.Encoding.UTF8);
                string buffer = reader.ReadToEnd();
                return buffer;
            }
            else
                return null;
        }
        catch (Exception e)
        {
            //System.Console.WriteLine("Can't download:" + e);
            return null;
        }
        finally
        {
            if (reader != null)
                reader.Close();

            if (stream != null)
                stream.Close();

            if (response != null)
                response.Close();
        }
    }
    
    public static string DecodeContent(byte[] byteInput, Encoding enc)
    {
        Decoder decoder = enc.GetDecoder();
        char[] chars = new char[byteInput.Length];
        byte[] bytes = new byte[byteInput.Length * 4];

        bool completed = false;
        int byteIndex = 0;
        int bytesUsed;
        int charsUsed;
        bool flush = false;

        decoder.Convert(byteInput, byteIndex, byteInput.Length,
                        chars, 0, byteInput.Length, flush,
                        out bytesUsed, out charsUsed, out completed);
        return new string(chars);
    }

    public static byte[] ReadFully(Stream input)
    {
        byte[] buffer = new byte[16 * 1024];
        using (MemoryStream ms = new MemoryStream())
        {
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, read);
            }
            return ms.ToArray();
        }
    }
}