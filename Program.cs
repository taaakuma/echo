using System;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;

class Program
{
    static void Main()
    {
        // デフォルトの録音デバイスと再生デバイスを取得
        var captureDevice = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
        var renderDevice = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        // 録音デバイスのフォーマットを自動取得
        var waveFormat = captureDevice.AudioClient.MixFormat;

        // キャプチャと再生の初期化
        using var capture = new WasapiCapture(captureDevice, true); // イベント駆動型キャプチャ
        using var output = new WasapiOut(renderDevice, AudioClientShareMode.Shared, false, 0); // バッファサイズ設定

        var bufferedWaveProvider = new BufferedWaveProvider(waveFormat)
        {
            BufferLength = 4096 * 8, // 適切なバッファサイズ
            DiscardOnBufferOverflow = true // バッファオーバーフロー時のデータ破棄
        };

        // エコー用の短めのバッファを用意 (0.25秒分のデータ)
        var echoBuffer = new float[waveFormat.SampleRate / 4 * waveFormat.Channels]; 
        int echoOffset = 0; // エコーバッファの位置
        float decay = 0.1f; // エコーの減衰率（低めに設定して目立ちにくく）

        // 録音データが利用可能なときのイベント処理
        capture.DataAvailable += (s, e) =>
        {
            try
            {
                byte[] buffer = e.Buffer;
                int bytesRecorded = e.BytesRecorded;
                float[] samples = new float[bytesRecorded / 4]; // 32-bit float形式の場合

                // バイトデータを浮動小数点データに変換
                Buffer.BlockCopy(buffer, 0, samples, 0, bytesRecorded);

                // エコーを加える
                for (int i = 0; i < samples.Length; i++)
                {
                    float inputSample = samples[i];
                    float echoSample = echoBuffer[echoOffset];
                    float outputSample = inputSample + echoSample * decay;

                    echoBuffer[echoOffset] = inputSample; // エコーバッファを更新
                    echoOffset = (echoOffset + 1) % echoBuffer.Length;

                    samples[i] = outputSample; // サンプルにエコーを反映
                }

                // 浮動小数点データをバイトデータに戻す
                Buffer.BlockCopy(samples, 0, buffer, 0, bytesRecorded);

                // バッファに追加
                bufferedWaveProvider.AddSamples(buffer, 0, bytesRecorded);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in processing: {ex.Message}");
            }
        };

        try
        {
            // 再生を初期化
            output.Init(bufferedWaveProvider);

            // 録音と再生を開始
            capture.StartRecording();
            output.Play();

            Console.WriteLine("リアルタイムエコーループバック開始（Enterで終了）");
            Console.ReadLine();
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            Console.WriteLine($"COM Exception: {ex.Message} (HRESULT: {ex.ErrorCode:X8})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            // 録音と再生の終了
            capture.StopRecording();
            output.Stop();
        }
    }
}
/*
using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

class Program
{
    static async Task Main()
    {
        // デフォルトの録音デバイスと再生デバイスを取得
        var captureDevice = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
        var renderDevice = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        // 録音デバイスのフォーマットを自動取得
        var waveFormat = captureDevice.AudioClient.MixFormat;

        // キャプチャと再生の初期化
        using var capture = new WasapiCapture(captureDevice, true); // イベント駆動型キャプチャ
        using var output = new WasapiOut(renderDevice, AudioClientShareMode.Shared, false, 1); // バッファサイズを設定

        var bufferedWaveProvider = new BufferedWaveProvider(waveFormat)
        {
            BufferLength = 5000, // 適切なバッファサイズ
            DiscardOnBufferOverflow = true // バッファオーバーフロー時のデータ破棄
        };

        // 録音データが利用可能なときのイベント処理
        capture.DataAvailable += (s, e) =>
        {
            try
            {
                bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded); // データをバッファに追加
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Buffer Error: {ex.Message}");
            }
        };

        // 非同期の再生処理
        var playbackTask = Task.Run(() =>
        {
            try
            {
                output.Init(bufferedWaveProvider);
                output.Play();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Playback Error: {ex.Message}");
            }
        });

        // 非同期の録音処理
        var recordingTask = Task.Run(() =>
        {
            try
            {
                capture.StartRecording();
                Console.WriteLine("リアルタイムオーディオループバック開始（Enterで終了）");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Recording Error: {ex.Message}");
            }
        });

        // メインスレッドで停止処理を制御
        await Task.WhenAny(
            Task.Run(() =>
            {
                Console.ReadLine(); // ユーザー入力を待機して終了
            })
        );

        // 録音と再生を停止
        capture.StopRecording();
        output.Stop();

        Console.WriteLine("終了しました。");
    }
}
*/