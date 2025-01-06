using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mauordle.Controls
{
    public class SuccessTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Success { get; set; }
        public DataTemplate Failed { get; set; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        {
            Attempt attempt = item as Attempt;
            return attempt.Success ? Success : Failed;
        }
    }


}
