﻿using IO = System.IO;
using System.IO;
using System.Threading.Tasks;
using CtrDotNet.Utility;

namespace CtrDotNet.CTR.Garc
{
	public class GarcFile : IGarcFile
	{
		#region Static

		public static async Task<GarcFile> FromFile( string path, bool useLz = false )
		{
			using ( var fs = new FileStream( path, FileMode.Open, FileAccess.Read, FileShare.Read ) )
			{
				byte[] buffer = new byte[ fs.Length ];
				await fs.ReadAsync( buffer, 0, buffer.Length );
				GarcFile file = new GarcFile( path, useLz );
				file.Read( buffer );
				return file;
			}
		}

		#endregion

		internal GarcFile( string p, bool useLz = false )
		{
			if ( useLz )
				this.GarcData = new LzGarc();
			else
				this.GarcData = new MemGarc();

			this.Path = p;
		}

		public int FileCount => this.GarcData.FileCount;
		public BaseGarc GarcData { get; }
		public string Path { get; }

		public void Read( byte[] data ) => this.GarcData.Read( data );

		public Task<byte[][]> GetFiles() => this.GarcData.GetFiles();

		public async Task<byte[]> GetFile( int file ) => ( await this.GetFiles() )[ file ];

		public Task SetFiles( byte[][] files ) => this.GarcData.SetFiles( files );

		public Task SetFile( int file, byte[] data ) => this.GarcData.SetFile( file, data );

		public Task<byte[]> Write() => this.GarcData.Write();

		public Task SaveFile() => this.SaveFileTo( PathUtil.GetPathBase( this.Path, "RomFS" ) );

		public async Task SaveFileTo( string path )
		{
			string filename = IO.Path.GetFileName( this.Path );

			await this.GarcData.SaveFile();

			using ( var fs = new FileStream( IO.Path.Combine( path, filename ), FileMode.Create, FileAccess.Write, FileShare.None ) )
				await fs.WriteAsync( this.GarcData.Data, 0, this.GarcData.Data.Length );
		}
	}
}