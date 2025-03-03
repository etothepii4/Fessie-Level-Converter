using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using FessieLevelConverter.Fessie;
using FessieLevelConverter.Parser;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Path = System.IO.Path;

namespace FessieLevelConverter;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    
    private String ImportPath = null;
    private ObservableCollection<string> FilesList = new ();
    private bool ToggleExportJson = false;
    private bool ToggleExportDat = false;
    private bool ToggleExportTmx = false;
    private String FessieTilesFilepath = Path.GetFullPath("./Fessie-Tiles.tsx");
    private bool TmxExportToggleEnabled = true;
    
    public MainWindow()
    {
        DataContext = this;
        InitializeComponent();
        FilesListView.ItemsSource = FilesList;
        ExportProgressBar.Visibility = Visibility.Collapsed;
        
        // check if tilesets are available
        if (!Path.Exists(FessieTilesFilepath))
        {
            TmxExportToggleEnabled = false;
            MessageBox.Show($"Das Tileset 'FessieTiles.tsx' befindet sich nicht am erwarteten Ort {FessieTilesFilepath}. *.tmx Export ist deaktiviert.", "Fehlendes Tileset für *.tmx Export", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
    
    private void ImportFiles(object sender, RoutedEventArgs e)
    {
        OpenFileDialog importFile = new OpenFileDialog();
        if(ImportPath != null)
        {
            importFile.InitialDirectory = ImportPath;
        }
        importFile.Multiselect = true;
        importFile.Filter = "Supported files (*.dat, *.json, *.tmx)|*.dat;*.json;*.tmx";
        
        if (importFile.ShowDialog() == true)
        {
            foreach (String filepath in importFile.FileNames)
            {
                var filename = Path.GetFileName(filepath);
                if (!FilesList.Contains(filepath))
                {
                    FilesList.Add(filepath);
                }
            }
        }
    }

    private void ClearFiles(object sender, RoutedEventArgs e)
    {
        FilesList.Clear();
        FilesListView.ItemsSource = null;
        FilesListView.ItemsSource = FilesList;
    }
    
    private void ExportFiles(object sender, RoutedEventArgs e)
    {
        CommonOpenFileDialog dialog = new CommonOpenFileDialog();
        dialog.IsFolderPicker = true;
        
        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            ExportProgressBar.Visibility = Visibility.Visible;
            foreach (String filepath in FilesList)
            {
                Task conversion = new Task(() => ConvertLevel(filepath, Path.Join(dialog.FileName, Path.GetFileNameWithoutExtension(filepath))));
                conversion.Start();
            }
            
            ExportProgressBar.Visibility = Visibility.Collapsed;
            MessageBox.Show($"Bearbeitung abgeschlossen.",
                "Bearbeitung abgeschlossen", MessageBoxButton.OK, MessageBoxImage.None);
        }
    }

    private void ConvertLevel(string inputFilepath, string outputFilepathWithoutExtension)
    {
        var filename = Path.GetFileName(inputFilepath);
        var extension = Path.GetExtension(inputFilepath);

        FessieLevel? level = null;
        if (extension == ".dat")
        {
            level = DatParser.ParseLevel(inputFilepath);
        } else if (extension == ".json")
        {
            level = JsonParser.ParseLevel(inputFilepath);
        } else if (extension == ".tmx")
        {
            level = TmxParser.ParseLevel(inputFilepath);
        }

        if (level is null)
        {
            return;
        }

        if (ToggleExportDat)
        {
            DatParser.BuildLevel(level, outputFilepathWithoutExtension + ".dat");
        }
        if (ToggleExportJson)
        {
            JsonParser.BuildLevel(level, outputFilepathWithoutExtension + ".json");
        }
        if (ToggleExportTmx)
        {
            TmxParser.BuildLevel(level,  outputFilepathWithoutExtension + ".tmx", FessieTilesFilepath);
        }
    }
    
    private void SetJsonExportTrue(object sender, RoutedEventArgs e)
    {
        ToggleExportJson = true;
    }
    
    private void SetJsonExportFalse(object sender, RoutedEventArgs e)
    {
        ToggleExportJson = false;
    }
    
    private void SetDatExportTrue(object sender, RoutedEventArgs e)
    {
        ToggleExportDat = true;
    }
    
    private void SetDatExportFalse(object sender, RoutedEventArgs e)
    {
        ToggleExportDat = false;
    }
    
    private void SetTmxExportTrue(object sender, RoutedEventArgs e)
    {
        ToggleExportTmx = true;
    }
    
    private void SetTmxExportFalse(object sender, RoutedEventArgs e)
    {
        ToggleExportTmx = false;
    }
}