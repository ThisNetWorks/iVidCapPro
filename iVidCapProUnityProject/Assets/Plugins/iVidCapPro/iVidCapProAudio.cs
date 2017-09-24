
/* -----------------------------------------------------------------------------------
 	-- iVidCapProAudio --
 	
 	To use this script, place it on the same game object as the AudioListener in the
 	scene.  Generally this is the Main Camera.
 	
 	To-Do:
 		- Change setting of STREAM_CAP_FREQ to AudioSettings.outputSampleRate, dependent
 		  on whether Save or SaveStreamStart is being used.
   ----------------------------------------------------------------------------------- */

/* ---------------------------------------------------------------------
   Change History:
   
   - 01 Dec 16 - Make change to SaveStreamStop to set _isRecording first
                 instead of waiting until the end. It is hoped this will
                 help prevent some of the sporadic crashes when the native
                 plugin is trying to load the audio file as an asset.
   - 22 Mar 13 - Add the muteSceneAudio property.
   - 15 Dec 12 - Comment out debug statements.
   --------------------------------------------------------------------- */

//  This script was derived from a script by Calvin Rien, posted to the Unity 
//  forums. This version of the script has been substantively modified by James Allen, eOe,
//  2012.
//
//  The modifications implement the ability to create a .wav file by capturing the
//  output of the AudioListener.  This provides the ability to create a .wav file that
//  is a mix of all the currently playing audio sources and is streamed to the target
//  file while the Unity app is being played.
//
//  The copyright statement from the original script is included below.
//
// =======================================================================================
//
//	Copyright (c) 2012 Calvin Rien http://the.darktable.com
//
//	This software is provided 'as-is', without any express or implied warranty. In
//	no event will the authors be held liable for any damages arising from the use
//	of this software.
//
//	Permission is granted to anyone to use this software for any purpose,
//	including commercial applications, and to alter it and redistribute it freely,
//	subject to the following restrictions:
//
//	1. The origin of this software must not be misrepresented; you must not claim
//	that you wrote the original software. If you use this software in a product,
//	an acknowledgment in the product documentation would be appreciated but is not
//	required.
//
//	2. Altered source versions must be plainly marked as such, and must not be
//	misrepresented as being the original software.
//
//	3. This notice may not be removed or altered from any source distribution.
//
//  =============================================================================
//
//  derived from Gregorio Zanon's script
//  http://forum.unity3d.com/threads/119295-Writing-AudioListener.GetOutputData-to-wav-problem?p=806734&viewfull=1#post806734
//  

/*------------------------------------------------------------------------------*/
/**
	@file 		iVidCapProAudio.cs
	@brief 		Place this script on the same game object as the AudioListener
	
--------------------------------------------------------------------------------*/

using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// This class supports the audio capture feature of the iVidCap Pro plugin.
/// Its use is optional and is needed only when you want to record audio.
/// 
/// To use it, place it on the same game object as the AudioListener in the
/// scene.  Often this will be the Main Camera object, but may be anywhere.
/// 
/// The "gain" property may be used to adjust the volume of the recorded track.
/// 
/// There are no methods that the client application needs to invoke in this
/// class.
/// </summary>
///
public class iVidCapProAudio : MonoBehaviour {
	
	/// <summary>
	/// Use this property to adjust the volume of the recorded audio track. 
	/// This enables you to change the volume of the recorded track without
	/// changes the volume of audio sources in the scene.
	/// </summary>
	public float gain = 1.0f;
	
	/// <summary>
	/// Use this property to mute the playback of scene audio on the device while
	/// recording scene audio to the video.  This is especially useful when using the 
	/// mic to record a voiceover.  By using this feature you can get a clean vocal
	/// recording without having the scene audio feeding into the mic. 
	/// </summary>
	public bool muteSceneAudio = false;
	
	private const int HEADER_SIZE = 44;
	//private const int STREAM_CAP_FREQ  = 44100;
	private int STREAM_CAP_FREQ  = 24000;
	
	private FileStream _fileStream;
	
	private bool _isRecording = false;
	
	private int _numStreamSamples = 0;
	private int _numStreamChannels = 0;
	
	// Lock to protect _fileStream.  OnAudioFilterRead runs in the audio thread
	// not the main thread.  We need to serialize access to the wave file handle.
	private static readonly object _locker = new object();
	
	
	/* -------------------------------------------------------------------------
	 	-- SaveStreamStart --
	 	
	 	Use this method to start saving the current Audio stream as heard by the 
	 	AudioListener object.  The audio stream data will be written to the
	 	specified .wav file.  This is useful when you want to playback multiple
	 	audio sources and have them combined into a single .wav file.
	   ------------------------------------------------------------------------- */
	public bool SaveStreamStart(string path, string filename) {
		
		// This should work but currently causes problems on iOS devices.
		//AudioSettings.outputSampleRate = 44100;
		
		// Make sure this is set properly!  It is different between in-editor and
		// on device.
		STREAM_CAP_FREQ = AudioSettings.outputSampleRate;
		
		if (!filename.ToLower().EndsWith(".wav")) {
			filename += ".wav";
		}

		var filepath = Path.Combine(path, filename);

		//Debug.Log ("SaveStreamStart: initializing audio stream write to: " + filepath);
		//Debug.Log ("SaveStreamStart: audio settings sample rate = " + AudioSettings.outputSampleRate);
		//Debug.Log ("SaveStreamStart: audio settings driverCaps = " + AudioSettings.driverCaps);

		// Make sure directory exists if user is saving to sub dir.
		Directory.CreateDirectory(Path.GetDirectoryName(filepath));

		//print("temp audio file => " + filepath);

		if (!_isRecording)
        {
        	CreateEmpty(filepath);
            _isRecording = true;
        }

		return true; // TO-DO: return false if there's a failure saving the file
	}
	
	/* -------------------------------------------------------------------------
	 	-- SaveStreamStop --
	 	
	 	Use this method to stop saving the current Audio stream as heard by the 
	 	AudioListener object.  
	   ------------------------------------------------------------------------- */
	public bool SaveStreamStop() {
		
		if (_isRecording)
        {
			// Make sure we do this first.  We don't want OnAudioFilterRead
			// to do any processing if it is invoked by the audio thread while
			// the audio file header is being written.
			_isRecording = false;

			//Debug.Log ("SaveStreamStop: finalizing audio stream write");
			lock (_locker) {
				WriteHeader(STREAM_CAP_FREQ, _numStreamChannels, _numStreamSamples/2);
        		_fileStream.Close ();
				_fileStream = null;
			}
            
        }

		return true; // TO-DO: return false if there's a failure saving the file
	}
	
	public void OnAudioFilterRead(float[] samples, int channels)
    {
		//Debug.Log ("OnAudioFilterRead called....");
        if(_isRecording)
        {
            ConvertAndWrite(samples);
			_numStreamSamples += samples.Length;
			_numStreamChannels = channels;
        }
    }
	
	private void CreateEmpty(string filepath) {
		
		_fileStream = new FileStream(filepath, FileMode.Create);
	    byte emptyByte = new byte();

	    for(int i = 0; i < HEADER_SIZE; i++) //preparing the header
	    {
	        _fileStream.WriteByte(emptyByte);
	    }
	}
	
	private void ConvertAndWrite(float[] samples) {
		
		Int16[] intData = new Int16[samples.Length];
		//converting in 2 float[] steps to Int16[], //then Int16[] to Byte[]

		Byte[] bytesData = new Byte[samples.Length * 2];
		//bytesData array is twice the size of
		//dataSource array because a float converted in Int16 is 2 bytes.

		int rescaleFactor = 32767; //to convert float to Int16

		for (int i = 0; i<samples.Length; i++) {
			intData[i] = (short) ((samples[i] * gain) * rescaleFactor);
			Byte[] byteArr = new Byte[2];
			byteArr = BitConverter.GetBytes(intData[i]);
			byteArr.CopyTo(bytesData, i * 2);
			
			if (muteSceneAudio)
				samples[i] = 0.0f;
		}
		
		// OnAudioFilterRead runs in the audio thread.  Occasionally it will
		// call this method even after the filestream has been closed.  Make
		// sure we don't try to use it after it's been closed.
		lock (_locker) {
			if (_fileStream != null) {
				_fileStream.Write(bytesData, 0, bytesData.Length);
			} 
		}
	}
	
	private void WriteHeader(int freq, int channels, int numSamples) {

		//Debug.Log("WriteHeader: freq=" + freq + " channels=" + channels + " numSamples=" + numSamples);

		_fileStream.Seek(0, SeekOrigin.Begin);

		Byte[] riff = System.Text.Encoding.UTF8.GetBytes("RIFF");
		_fileStream.Write(riff, 0, 4);

		Byte[] chunkSize = BitConverter.GetBytes(_fileStream.Length - 8);
		_fileStream.Write(chunkSize, 0, 4);

		Byte[] wave = System.Text.Encoding.UTF8.GetBytes("WAVE");
		_fileStream.Write(wave, 0, 4);

		Byte[] fmt = System.Text.Encoding.UTF8.GetBytes("fmt ");
		_fileStream.Write(fmt, 0, 4);

		Byte[] subChunk1 = BitConverter.GetBytes(16);
		_fileStream.Write(subChunk1, 0, 4);

		//UInt16 two = 2;
		UInt16 one = 1;

		Byte[] audioFormat = BitConverter.GetBytes(one);
		_fileStream.Write(audioFormat, 0, 2);

		Byte[] numChannels = BitConverter.GetBytes(channels);
		_fileStream.Write(numChannels, 0, 2);

		Byte[] sampleRate = BitConverter.GetBytes(freq);
		_fileStream.Write(sampleRate, 0, 4);
		
		// sampleRate * bytesPerSample * number of channels, generally 44100*2*2
		Byte[] byteRate = BitConverter.GetBytes(freq * 2 * channels); 
		_fileStream.Write(byteRate, 0, 4);

		UInt16 blockAlign = (ushort) (channels * 2);
		_fileStream.Write(BitConverter.GetBytes(blockAlign), 0, 2);

		UInt16 bps = 16;
		Byte[] bitsPerSample = BitConverter.GetBytes(bps);
		_fileStream.Write(bitsPerSample, 0, 2);

		Byte[] datastring = System.Text.Encoding.UTF8.GetBytes("data");
		_fileStream.Write(datastring, 0, 4);

		//Byte[] subChunk2 = BitConverter.GetBytes(numSamples * channels * 2);
		Byte[] subChunk2 = BitConverter.GetBytes(_fileStream.Length - HEADER_SIZE);
		_fileStream.Write(subChunk2, 0, 4);

	}
}