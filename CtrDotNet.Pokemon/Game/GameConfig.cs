﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CtrDotNet.CTR;
using CtrDotNet.CTR.Garc;
using CtrDotNet.Pokemon.Cro;
using CtrDotNet.Pokemon.ExeFS;
using CtrDotNet.Pokemon.Garc;
using CtrDotNet.Pokemon.Reference;
using CtrDotNet.Pokemon.Structures.CRO.Gen6.Starters;
using CtrDotNet.Pokemon.Structures.ExeFS.Common;
using CtrDotNet.Pokemon.Structures.RomFS.Common;
using CtrDotNet.Pokemon.Structures.RomFS.PokemonInfo;
using CtrDotNet.Pokemon.Utility;
using Gen6 = CtrDotNet.Pokemon.Structures.RomFS.Gen6;
using Gen7 = CtrDotNet.Pokemon.Structures.RomFS.Gen7;

namespace CtrDotNet.Pokemon.Game
{
	public class GameConfig
	{
		private const int FilecountXy = 271;
		private const int FilecountOrasDemo = 301;
		private const int FilecountOras = 299;
		private const int FilecountSmDemo = 239;
		private const int FilecountSm = 311; // only a guess for now

		public GameConfig( int fileCount )
		{
			GameVersion game = GameVersion.Invalid;

			switch ( fileCount )
			{
				case FilecountXy:
					game = GameVersion.XY;
					break;
				case FilecountOrasDemo:
					game = GameVersion.ORASDemo;
					break;
				case FilecountOras:
					game = GameVersion.ORAS;
					break;
				case FilecountSmDemo:
					game = GameVersion.SunMoonDemo;
					break;
				case FilecountSm:
					game = GameVersion.SunMoon;
					break;
			}

			if ( game == GameVersion.Invalid )
				throw new InvalidOperationException( $"Invalid game version. No game known with {fileCount} files." );

			this.Version = game;
		}

		public GameConfig( GameVersion game )
		{
			this.Version = game;
		}

		#region Properties

		public GameVersion Version { get; }
		public CodeBin CodeBin { get; private set; }
		public GarcReference[] Files { get; private set; }
		public TextVariableCode[] Variables { get; private set; }
		public TextReference[] GameText { get; private set; }
		public string[][] GameTextStrings { get; private set; }
		public string RomFS { get; private set; }
		public string ExeFS { get; private set; }
		public Language Language { get; set; }
		public string OutputPathOverride { get; set; }
		public bool IsOverridingOutPath => !string.IsNullOrEmpty( this.OutputPathOverride );

		#endregion

		#region Initialization

		private void GetGameData( GameVersion game )
		{
			switch ( game )
			{
				case GameVersion.XY:
					this.Files = ReferenceLists.Garc.Xy;
					this.Variables = ReferenceLists.Variables.Xy;
					this.GameText = ReferenceLists.Text.Xy;
					break;

				case GameVersion.ORASDemo:
				case GameVersion.ORAS:
					this.Files = ReferenceLists.Garc.Oras;
					this.Variables = ReferenceLists.Variables.Oras;
					this.GameText = ReferenceLists.Text.Oras;
					break;

				case GameVersion.SunMoonDemo:
					this.Files = ReferenceLists.Garc.SunMoonDemo;
					this.Variables = ReferenceLists.Variables.SunMoon;
					this.GameText = ReferenceLists.Text.SunMoonDemo;
					break;
				case GameVersion.SunMoon:
					this.Files = ReferenceLists.Garc.Sun;
					if ( new FileInfo( Path.Combine( this.RomFS, this.GetGarcFileName( GarcNames.EncounterData ) ) ).Length == 0 )
						this.Files = ReferenceLists.Garc.Moon;
					this.Variables = ReferenceLists.Variables.SunMoon;
					this.GameText = ReferenceLists.Text.SunMoon;
					break;
			}
		}

		public async Task Initialize( string romFSpath, string exeFSpath, Language lang )
		{
			this.RomFS = romFSpath;
			this.ExeFS = exeFSpath;
			this.Language = lang;

			this.GetGameData( this.Version );

			await this.GetCodeBin();
		}

		#endregion

		#region Game Data

		public Task SaveFile( IWritableFile file ) => this.IsOverridingOutPath
														  ? file.SaveFileTo( this.OutputPathOverride )
														  : file.SaveFile();

		#region Pokemon Species Info

		public async Task<PokemonInfoTable> GetPokemonInfo()
		{
			PokemonInfoTable table = new PokemonInfoTable( this.Version );
			var garcPersonal = await this.GetGarc( GarcNames.PokemonInfo );
			byte[] data = await garcPersonal.GetFile( garcPersonal.Garc.FileCount - 1 );
			table.Read( data );
			return table;
		}

		public async Task SavePokemonInfo( PokemonInfoTable table )
		{
			Assertions.AssertVersion( this.Version, table.GameVersion );

			var garcPersonal = await this.GetGarc( GarcNames.PokemonInfo );
			byte[][] files = await garcPersonal.GetFiles();

			files[ garcPersonal.Garc.FileCount - 1 ] = table.Write();

			await garcPersonal.SetFiles( files );
			await this.SaveFile( garcPersonal );
		}

		#endregion

		#region Learnsets

		public async Task<IEnumerable<Learnset>> GetLearnsets()
		{
			var garcLearnsets = await this.GetGarc( GarcNames.Learnsets );
			byte[][] files = await garcLearnsets.GetFiles();
			return files.Select( f => {
				Learnset l;

				if ( this.Version.IsGen6() )
					l = new Gen6.Learnset( this.Version );
				else
					l = new Gen7.Learnset( this.Version );

				l.Read( f );
				return l;
			} );
		}

		public async Task SaveLearnsets( IList<Learnset> learnsets )
		{
			var garcLearnsets = await this.GetGarc( GarcNames.Learnsets );

			foreach ( Learnset l in learnsets )
				Assertions.AssertVersion( this.Version, l.GameVersion );

			byte[][] files = learnsets.Select( l => l.Write() ).ToArray();

			await garcLearnsets.SetFiles( files );
			await this.SaveFile( garcLearnsets );
		}

		#endregion

		#region Game Text

		public async Task<TextFile[]> GetGameText()
		{
			var garcGameText = await this.GetGarc( GarcNames.GameText );
			byte[][] files = await garcGameText.GetFiles();

			return files.Select( file => {
				TextFile tf = new TextFile( this.Version, this.Variables );
				tf.Read( file );
				return tf;
			} ).ToArray();
		}

		public async Task<TextFile> GetGameText( int textFile )
		{
			var garcGameText = await this.GetGarc( GarcNames.GameText );
			byte[] file = await garcGameText.GetFile( textFile );

			TextFile tf = new TextFile( this.Version, this.Variables );
			tf.Read( file );
			return tf;
		}

		public async Task SaveGameText( IEnumerable<TextFile> textFiles )
		{
			var garcGameText = await this.GetGarc( GarcNames.GameText );
			byte[][] files = textFiles.Select( tf => tf.Write() ).ToArray();

			await garcGameText.SetFiles( files );
			await this.SaveFile( garcGameText );
		}

		public async Task SaveGameText( int fileNum, TextFile textFile )
		{
			var garcGameText = await this.GetGarc( GarcNames.GameText );

			await garcGameText.SetFile( fileNum, textFile.Write() );
			await this.SaveFile( garcGameText );
		}

		#endregion

		#region Moves

		public async Task<IEnumerable<Move>> GetMoves()
		{
			var garcMoves = await this.GetGarc( GarcNames.Moves );
			IList<byte[]> files = await garcMoves.GetFiles();

			if ( this.Version.IsORAS() || this.Version.IsGen7() )
			{
				files = Mini.UnpackMini( files[ 0 ], "WD" );
			}

			return files.Select( file => {
				Move m = new Move( this.Version );
				m.Read( file );
				return m;
			} );
		}

		public async Task SaveMoves( IList<Move> moves )
		{
			var garcMoves = await this.GetGarc( GarcNames.Moves );

			foreach ( var move in moves )
				Assertions.AssertVersion( this.Version, move.GameVersion );

			if ( this.Version.IsORAS() || this.Version.IsGen7() )
			{
				byte[] file = Mini.PackMini( moves.Select( m => m.Write() ).ToArray(), "WD" );
				await garcMoves.SetFile( 0, file );
			}
			else
			{
				await garcMoves.SetFiles( moves.Select( m => m.Write() ).ToArray() );
			}

			await this.SaveFile( garcMoves );
		}

		#endregion

		#region Encounter Data

		public async Task<IEnumerable<Gen6.EncounterWild>> GetEncounterData()
		{
			var encounterGarc = await this.GetGarc( GarcNames.EncounterData, useLz: true );

			// All but last two files are the encounter data
			byte[][] encounterBuffers = ( await encounterGarc.GetFiles() ).Take( encounterGarc.Garc.FileCount - 2 ).ToArray();

			// Second-to-last file is zone data
			byte[] zoneDataBuffer = await encounterGarc.GetFile( encounterGarc.Garc.FileCount - 2 );
			Gen6.ZoneData zoneData = new Gen6.ZoneData( this.Version );
			zoneData.Read( zoneDataBuffer );

			if ( zoneData.Entries.Length != encounterBuffers.Length )
				throw new InvalidDataException( $"Zone data and encounter data mismatch. Zone data had {zoneData.Entries.Length} entries, but encounter data only had {encounterBuffers.Length}" );

			return encounterBuffers.Zip( zoneData.Entries, ( b, e ) => {
				var encounter = Gen6.EncounterWild.New( this.Version, e.ZoneId );
				encounter.Read( b );
				return encounter;
			} );
		}

		public async Task SaveEncounterData( IEnumerable<Gen6.EncounterWild> encounters )
		{
			var encounterGarc = await this.GetGarc( GarcNames.EncounterData, useLz: true );
			Gen6.EncounterWild[] array = encounters.ToArray();
			byte[][] files = await encounterGarc.GetFiles();

			Assertions.AssertLength( encounterGarc.Garc.FileCount - 2, array, exact: true );

			for ( int i = 0; i < array.Length; i++ )
			{
				byte[] encounterBuffer = array[ i ].Write();

				if ( this.Version.IsORAS() ) // Not really sure why this is necessary, but pk3DS does it
				{
					// Last file is decStorage
					const int offset = 0xE;
					byte[] decStorageData = files[ files.Length - 1 ];
					int entryPointer = BitConverter.ToInt32( decStorageData, ( i + 1 ) * sizeof( int ) ) + offset;

					Array.Copy( encounterBuffer, 0, decStorageData, entryPointer, encounterBuffer.Length - offset );
				}

				files[ i ] = encounterBuffer;
			}

			await encounterGarc.SetFiles( files );
			await this.SaveFile( encounterGarc );
		}

		#endregion

		#region TMs/HMs

		public TmsHms GetTmsHms()
		{
			TmsHms tmsHms = new TmsHms( this.Version );
			tmsHms.ReadFromCodeBin( this.CodeBin );
			return tmsHms;
		}

		public async Task SaveTmsHms( TmsHms tmsHms )
		{
			Assertions.AssertVersion( this.Version, tmsHms.GameVersion );

			this.CodeBin.WriteStructure( tmsHms );

			await this.SaveFile( this.CodeBin );
		}

		#endregion

		#region Starters

		public async Task<Starters> GetStarters()
		{
			CroFile dllField = await this.GetCroFile( CroNames.Field );
			CroFile dllPokeSelect = await this.GetCroFile( CroNames.Poke3Select );
			Starters starters = new Starters( this.Version );

			await starters.Read( dllField, dllPokeSelect );

			return starters;
		}

		public async Task SaveStarters( Starters starters )
		{
			CroFile dllField = await this.GetCroFile( CroNames.Field );
			CroFile dllPokeSelect = await this.GetCroFile( CroNames.Poke3Select );

			await starters.Write( dllField, dllPokeSelect );

			await this.SaveFile( dllField );
			await this.SaveFile( dllPokeSelect );
		}

		#endregion

		#endregion

		#region CRO

		public Task<CroFile> GetCroFile( CroNames name )
		{
			string path = Path.Combine( this.RomFS, $"Dll{name}.cro" );

			if ( !File.Exists( path ) )
				throw new FileNotFoundException( $"CRO file not found: {name}" );

			return CroFile.FromFile( path );
		}

		#endregion

		#region code.bin

		private async Task GetCodeBin()
		{
			string path = Directory.GetFiles( this.ExeFS, "*code.bin" ).FirstOrDefault();

			if ( path == null )
				throw new FileNotFoundException( "Could not find code binary file" );

			this.CodeBin = new CodeBin( path );
			await this.CodeBin.Load();
		}

		#endregion

		#region Garc

		public GarcReference GetGarcReference( GarcNames garcName )
		{
			var garcRef = this.Files.FirstOrDefault( f => f.Name == garcName );

			if ( garcRef == null )
				throw new FileNotFoundException( $"GARC file not found: {garcName}" );

			if ( garcRef.HasLanguageVariant )
				garcRef = garcRef.GetRelativeGarc( (int) this.Language );

			return garcRef;
		}

		public string GetGarcFileName( GarcNames garcName )
			=> this.GetGarcReference( garcName ).RomFsPath;

		private string GetGarcPath( GarcNames garcName )
			=> Path.Combine( this.RomFS,
							 this.GetGarcReference( garcName ).RomFsPath );

		public async Task<ReferencedGarc> GetGarc( GarcReference gr, bool useLz = false )
		{
			string path = this.GetGarcPath( gr.Name );
			GarcFile file = await GarcFile.FromFile( path, useLz );
			return new ReferencedGarc( file, gr );
		}

		public async Task<ReferencedGarc> GetGarc( GarcNames garcName, bool useLz = false )
		{
			var gr = this.GetGarcReference( garcName );
			return await this.GetGarc( gr, useLz );
		}

		#endregion

		public TextVariableCode GetVariableCode( string name ) => this.Variables.FirstOrDefault( v => v.Name == name );

		public TextVariableCode GetVariableName( int value ) => this.Variables.FirstOrDefault( v => v.Code == value );

		public TextReference GetGameText( TextNames name ) => this.GameText.FirstOrDefault( f => f.Name == name );

		public bool IsRebuildable( int fileCount )
		{
			switch ( fileCount )
			{
				case FilecountXy:
					return this.Version == GameVersion.XY;
				case FilecountOras:
					return this.Version == GameVersion.ORAS;
				case FilecountOrasDemo:
					return this.Version == GameVersion.ORASDemo;
				case FilecountSmDemo:
					return this.Version == GameVersion.SunMoonDemo;
				case FilecountSm:
					return this.Version == GameVersion.SunMoon;
			}
			return false;
		}
	}
}