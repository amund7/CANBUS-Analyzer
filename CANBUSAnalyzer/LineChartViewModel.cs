using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace WpfApplication.ViewModels
{
  public class LineChartViewModel : BaseNotifyPropertyChanged, ILineChartViewModel
  {
    private readonly IChartRepository _chartRepository;

    public LineChartViewModel(IChartRepository chartRepository)
    {
      chartRepository.ThrowIfNull(nameof(chartRepository));
      _chartRepository = chartRepository;
      chartRepository.PropertyChanged += ChartRepositoryPropertyChanged;
    }

    private void ChartRepositoryPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
      if (nameof(IChartRepository.LineCountList).Equals(e.PropertyName))
      {
        OnPropertyChanged(nameof(CountList));
      }
    }

    public IReadOnlyList<DataPoint> CountList =>
      _chartRepository.LineCountList.Select((value, index) => new DataPoint(index, value)).ToList();
  }
}
