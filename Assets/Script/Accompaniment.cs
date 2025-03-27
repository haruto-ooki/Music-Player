// SineWavePlayer.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Accompaniment : MonoBehaviour
{
    public int sampleRate = 44100;  // サンプルレート
    public float BPM = 150f;        // BPM（四分音符を基準）

    // 音名とインデックスのマッピング（C=0, C#(C+)=1, D (または D-)=2, D+(または E-)=3, ...）
    private Dictionary<string, int> noteIndices = new Dictionary<string, int>
    {
        { "C", 0 }, { "C+", 1 }, { "D-", 1}, { "D", 2 }, { "D+", 3 }, { "E-", 3},
        { "E", 4 }, { "F", 5 }, { "F+", 6 }, { "G-", 6}, { "G", 7 },
        { "G+", 8 }, { "A-", 8}, { "A", 9 }, { "A+", 10 }, { "B-", 10}, { "B", 11 }
    };

    /// <summary>
    /// 分音符指定（例："4"、"4."、"4~"）とBPMから秒数を計算する
    /// 例：
    ///  - "4"   → 四分音符 = 60/BPM 秒
    ///  - "2"   → 二分音符 = (4/2)*(60/BPM) 秒
    ///  - "8"   → 八分音符 = (4/8)*(60/BPM) 秒
    ///  - "4."  → 付点四分音符 = (60/BPM)*1.5 秒
    ///  - "4~"  → タイ指定付き（ここではタイ部分は後のノートと連結するため、その個々の値も同じ計算）
    /// ※ 本メソッドでは "~" は除去して数値部分のみで計算します。
    /// </summary>
    public float GetDuration(string noteDuration, float BPM)
    {
        bool dotted = noteDuration.Contains(".");
        // タイ指定の記号 "~" も除去
        string pureDuration = noteDuration.Replace(".", "").Replace("~", "");
        float noteValue;
        if (!float.TryParse(pureDuration, out noteValue))
        {
            Debug.LogError("Invalid note duration format: " + noteDuration);
            return 0f;
        }
        // 四分音符の長さは 60/BPM 秒、基準は4分音符なので(4 / noteValue) * (60/BPM)
        float duration = (4f / noteValue) * (60f / BPM);
        if (dotted)
        {
            duration *= 1.5f;
        }
        return duration;
    }

    /// <summary>
    /// 音名とオクターブから、A4=442Hzを基準とした周波数を計算する
    /// ここでは、Aが9、CはAより9半音下（0-indexed）として計算
    /// </summary>
    private float GetFrequency(int octave, string noteName)
    {
        if (!noteIndices.ContainsKey(noteName))
        {
            Debug.LogError("Invalid note name: " + noteName);
            return 0f;
        }
        int noteIndex = noteIndices[noteName];
        int semitoneOffset = noteIndex - noteIndices["A"]; // A からの相対半音差
        int octaveDiff = octave - 4; // 基準はオクターブ4 (A4)
        float frequency = 442f * Mathf.Pow(2f, (semitoneOffset + (octaveDiff * 12)) / 12f);
        return frequency;
    }

    /// <summary>
    /// 単音（サイン波）を、音名・オクターブ・分音符指定で再生する
    /// </summary>
    public void PlaySineTone(string noteName, int octave, string noteDuration, float BPM)
    {
        float duration = GetDuration(noteDuration, BPM);
        float frequency = GetFrequency(octave, noteName);

        AudioSource audioSource = GetComponent<AudioSource>();
        int sampleLength = Mathf.CeilToInt(sampleRate * duration);
        AudioClip clip = AudioClip.Create("SineTone_" + noteName + octave, sampleLength, 1, sampleRate, false);
        float[] samples = new float[sampleLength];
        for (int i = 0; i < sampleLength; i++)
        {
            samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / sampleRate);
        }
        clip.SetData(samples, 0);
        audioSource.clip = clip;
        audioSource.Play();
    }

    /// <summary>
    /// 和音を、各ノート情報（オクターブと音名・分音符指定のペア）の配列で再生する
    /// 各要素は { noteDuration, noteName, octave } の形式
    /// 例:
    ///   new string[][] {
    ///       new string[] { "4", "C", "4" },
    ///       new string[] { "4", "E", "4" },
    ///       new string[] { "4", "G", "4" },
    ///       new string[] { "4", "C", "5" }
    ///   }
    /// 
    /// ※ 本メソッドでは、各ノートは個別に生成され、合成された AudioClip で和音が再生されます。
    /// </summary>
    public void PlaySineChord(string[][] noteInfos, int defaultOctave, string noteDuration, float BPM)
    {
        float duration = GetDuration(noteDuration, BPM);
        int sampleLength = Mathf.CeilToInt(sampleRate * duration);
        AudioClip clip = AudioClip.Create("SineChord", sampleLength, 1, sampleRate, false);
        float[] samples = new float[sampleLength];

        // 各ノートについて、noteInfos の配列からオクターブ (第一要素) と音名 (第二要素) を取得して周波数算出
        List<float> frequencies = new List<float>();
        foreach (string[] info in noteInfos)
        {
            int octave;
            if (!int.TryParse(info[0], out octave))
            {
                octave = defaultOctave;
            }
            string noteName = info[1];
            float freq = GetFrequency(octave, noteName);
            frequencies.Add(freq);
        }

        // 全サンプルで各周波数のサイン波を合成（平均化）
        for (int i = 0; i < sampleLength; i++)
        {
            float sampleSum = 0f;
            foreach (float freq in frequencies)
            {
                sampleSum += Mathf.Sin(2 * Mathf.PI * freq * i / sampleRate);
            }
            samples[i] = sampleSum / frequencies.Count;
        }
        clip.SetData(samples, 0);
        AudioSource audioSource = GetComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.Play();
    }

    /// <summary>
    /// タイ機能付きの再生を行う
    /// tiedSequence の各要素は { noteDuration, noteName, octave } の形式となる。
    /// duration に "~" が含まれている場合、同一音（noteName と octave が一致）の連続として duration を連結する。
    /// 連結後、１つの AudioClip に合成して再生します。
    /// </summary>
    public void PlayTiedSineSequence(string[][] tiedSequence, float BPM)
    {
        List<float> overallSamples = new List<float>();

        bool segmentInitialized = false;
        string currentNoteName = "";
        int currentOctave = 0;
        float currentDuration = 0f;

        foreach (string[] note in tiedSequence)
        {
            if (note.Length < 3)
            {
                Debug.LogError("Invalid tied note specification.");
                continue;
            }

            string durStr = note[0];  // 例："4", "4~", "4.~" など
            string noteName = note[1];
            int octave;
            if (!int.TryParse(note[2], out octave))
            {
                octave = 4;
            }
            float durSec = GetDuration(durStr, BPM);
            bool tied = durStr.Contains("~");

            if (!segmentInitialized)
            {
                currentNoteName = noteName;
                currentOctave = octave;
                currentDuration = durSec;
                segmentInitialized = true;
            }
            else
            {
                // もし同一の音（音名 と オクターブが同じ）なら連結
                if (noteName == currentNoteName && octave == currentOctave)
                {
                    currentDuration += durSec;
                }
                else
                {
                    // 別の音なら、前のセグメントを確定して追加し、新しいセグメント開始
                    AppendSineToneToList(currentNoteName, currentOctave, currentDuration, overallSamples);
                    currentNoteName = noteName;
                    currentOctave = octave;
                    currentDuration = durSec;
                }
            }
            // タイ指定でなければ、セグメントを確定
            if (!tied)
            {
                AppendSineToneToList(currentNoteName, currentOctave, currentDuration, overallSamples);
                segmentInitialized = false;
                currentDuration = 0f;
                currentNoteName = "";
                currentOctave = 0;
            }
        }
        // ループ後、未確定のセグメントがあれば追加
        if (segmentInitialized)
        {
            AppendSineToneToList(currentNoteName, currentOctave, currentDuration, overallSamples);
        }

        int totalSamples = overallSamples.Count;
        AudioClip clipAll = AudioClip.Create("TiedSineSequence", totalSamples, 1, sampleRate, false);
        clipAll.SetData(overallSamples.ToArray(), 0);
        AudioSource audioSourceFinal = GetComponent<AudioSource>();
        audioSourceFinal.clip = clipAll;
        audioSourceFinal.Play();
    }

    /// <summary>
    /// 指定した音（noteName, octave, duration）のサイン波を生成し、samplesList に追加する
    /// </summary>
    private void AppendSineToneToList(string noteName, int octave, float duration, List<float> samplesList)
    {
        float frequency = GetFrequency(octave, noteName);
        int numSamples = Mathf.CeilToInt(sampleRate * duration);
        for (int i = 0; i < numSamples; i++)
        {
            float sample = Mathf.Sin(2 * Mathf.PI * frequency * i / sampleRate);
            samplesList.Add(sample);
        }
    }

    IEnumerator Start()
    {
        PlaySineTone("C", 0, "4", BPM);
        yield return new WaitForSeconds(GetDuration("4", BPM));
        // ------------------ここから下に書き込む-------------------------------
        PlaySineChord(new string[][]
        {
            new string[] {"3", "G"},
            new string[] {"3", "B-"}
        }, 3, "8", BPM);
        yield return new WaitForSeconds(GetDuration("8", BPM));

        yield return new WaitForSeconds(GetDuration("8", BPM));

        yield return new WaitForSeconds(GetDuration("4", BPM));

        PlaySineChord(new string[][]
        {
            new string[] {"3", "G"},
            new string[] {"3", "B-"},
            new string[] {"4", "F"}
        }, 3, "8", BPM);
        yield return new WaitForSeconds(GetDuration("8", BPM));

        yield return new WaitForSeconds(GetDuration("16", BPM));

        PlaySineChord(new string[][]
        {
            new string[] {"3", "G"},
            new string[] {"3", "B-"},
            new string[] {"4", "F"}
        }, 3, "16", BPM);
        yield return new WaitForSeconds(GetDuration("16", BPM));

        yield return new WaitForSeconds(GetDuration("8", BPM));

        PlaySineChord(new string[][]
        {
            new string[] {"3", "D"},
            new string[] {"3", "F"},
            new string[] {"4", "C"}
        }, 3, "8", BPM);
        yield return new WaitForSeconds(GetDuration("8", BPM));



        yield return new WaitForSeconds(GetDuration("8", BPM));

        PlaySineChord(new string[][]
        {
            new string[] {"3", "D"},
            new string[] {"3", "F"},
            new string[] {"4", "C"}
        }, 3, "8", BPM);
        yield return new WaitForSeconds(GetDuration("8", BPM));

        PlaySineChord(new string[][]
        {
            new string[] {"3", "D"},
            new string[] {"3", "F"},
            new string[] {"4", "C"}
        }, 3, "8", BPM);
        yield return new WaitForSeconds(GetDuration("8", BPM));

        yield return new WaitForSeconds(GetDuration("8", BPM));

        PlaySineChord(new string[][]
        {
            new string[] {"3", "D"},
            new string[] {"3", "F"},
            new string[] {"4", "C"}
        }, 3, "8", BPM);
        yield return new WaitForSeconds(GetDuration("8", BPM));

        yield return new WaitForSeconds(GetDuration("8", BPM));

        yield return new WaitForSeconds(GetDuration("4", BPM));



        PlaySineChord(new string[][]
        {
            new string[] {"3", "E-"},
            new string[] {"3", "B-"}
        }, 3, "8", BPM);
        yield return new WaitForSeconds(GetDuration("8", BPM));

        yield return new WaitForSeconds(GetDuration("8", BPM));

        yield return new WaitForSeconds(GetDuration("8", BPM));

        PlaySineChord(new string[][]
        {
            new string[] {"3", "E-"},
            new string[] {"3", "B-"}
        }, 3, "8", BPM);
        yield return new WaitForSeconds(GetDuration("8", BPM));

        PlaySineChord(new string[][]
        {
            new string[] {"3", "E-"},
            new string[] {"3", "B-"}
        }, 3, "8", BPM);
        yield return new WaitForSeconds(GetDuration("8", BPM));

        yield return new WaitForSeconds(GetDuration("16", BPM));

        PlaySineChord(new string[][]
        {
            new string[] {"3", "E-"},
            new string[] {"3", "B-"}
        }, 3, "16", BPM);
        yield return new WaitForSeconds(GetDuration("16", BPM));

        yield return new WaitForSeconds(GetDuration("8", BPM));

        PlaySineChord(new string[][]
        {
            new string[] {"2", "B-"},
            new string[] {"3", "F"}
        }, 3, "8", BPM);
        yield return new WaitForSeconds(GetDuration("8", BPM));



        yield return new WaitForSeconds(GetDuration("8", BPM));

        PlaySineChord(new string[][]
        {
            new string[] {"2", "B-"},
            new string[] {"3", "F"}
        }, 3, "8", BPM);
        yield return new WaitForSeconds(GetDuration("8", BPM));

        PlaySineChord(new string[][]
        {
            new string[] {"2", "B-"},
            new string[] {"3", "F"}
        }, 3, "8", BPM);
        yield return new WaitForSeconds(GetDuration("8", BPM));

        yield return new WaitForSeconds(GetDuration("8", BPM));

        PlaySineChord(new string[][]
        {
            new string[] {"2", "B-"},
            new string[] {"3", "F"}
        }, 3, "8", BPM);
        yield return new WaitForSeconds(GetDuration("8", BPM));

        yield return new WaitForSeconds(GetDuration("16", BPM));

        PlaySineChord(new string[][]
        {
            new string[] {"3", "F+"},
            new string[] {"4", "C"}
        }, 3, "16", BPM);
        yield return new WaitForSeconds(GetDuration("16", BPM));

        yield return new WaitForSeconds(GetDuration("4", BPM));
    }
}
