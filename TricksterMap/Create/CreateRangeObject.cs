using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TricksterMap.Data;

namespace TricksterMap.Create
{
    public partial class CreateRangeObject : Form
    {
        public MapDataInfo Map = null;
        public RangeObjectForm RangeListForm = null;
        public Dictionary<string, int> types = new Dictionary<string, int>();
        public bool isEditing = false;
        public int EditIndex = -1;

        public CreateRangeObject()
        {
            InitializeComponent();

            foreach (var type in RangeObject.ValidTypes)
            {
                var typeName = RangeObject.GetTypeNameFromId(type);

                types.Add(typeName, type);

                cmbType.Items.Add(typeName);
            }

            if (RangeObject.ValidTypes.Length > 0)
            {
                cmbType.SelectedIndex = 0;
            }

            Text = Strings.CreateRangeObject;
            lblId.Text = Strings.ID;
            lblMapId.Text = Strings.MapID;
            lblType.Text = Strings.Type;
            lblX1.Text = Strings.X1Pos;
            lblY1.Text = Strings.Y1Pos;
            lblX2.Text = Strings.X2Pos;
            lblY2.Text = Strings.Y2Pos;
            btnCreate.Text = Strings.Create;

            this.SetFonts();
        }

        private void CreateRangeObject_Load(object sender, EventArgs e)
        {

        }

        public int GetTypeIdFromName(string name)
        {
            if (types.ContainsKey(name))
            {
                return types[name];
            }

            return -1;
        }

        private void AddOrUpdateObject()
        {
            try
            {
                var range = new RangeObject()
                {
                    Id = int.Parse(txtId.Text),
                    Type = GetTypeIdFromName(cmbType.Text),
                    Destination = int.Parse(txtMapId.Text),
                    X1 = int.Parse(txtX1.Text),
                    Y1 = int.Parse(txtY1.Text),
                    X2 = int.Parse(txtX2.Text),
                    Y2 = int.Parse(txtY2.Text)
                };

                if (isEditing)
                {
                    RangeListForm.RangeService.Update(Map, EditIndex, range);
                }
                else
                {
                    RangeListForm.RangeService.Add(Map, range);
                }

                RangeListForm.RepopulateData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void CreateAndExit(object sender, EventArgs e)
        {
            AddOrUpdateObject();
            isEditing = false;
            Close();
        }

        private void CreateRangeObject_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (isEditing)
            {
                CreateAndExit(sender, e);
            }
        }
    }
}
