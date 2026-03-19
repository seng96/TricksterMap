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
    public partial class CreatePointObject : Form
    {
        public MapDataInfo Map = null;
        public PointObjectForm PointListForm = null;
        public Dictionary<string, int> types = new Dictionary<string, int>();
        public bool isEditing = false;
        public int EditIndex = -1;

        public CreatePointObject()
        {
            InitializeComponent();

            foreach (var type in PointObject.ValidTypes)
            {
                var typeName = PointObject.GetTypeNameFromId(type);

                types.Add(typeName, type);

                cmbType.Items.Add(typeName);
            }

            if (PointObject.ValidTypes.Length > 0)
            {
                cmbType.SelectedIndex = 0;
            }

            Text = Strings.CreatePointObject;
            lblId.Text = Strings.ID;
            lblMapId.Text = Strings.MapID;
            lblType.Text = Strings.Type;
            lblX.Text = Strings.XPos;
            lblY.Text = Strings.YPos;
            btnCreate.Text = Strings.Create;
            
            this.SetFonts();
        }

        private void CreatePointObject_Load(object sender, EventArgs e)
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
                var point = new PointObject()
                {
                    Id = int.Parse(txtId.Text),
                    Type = GetTypeIdFromName(cmbType.Text),
                    MapId = int.Parse(txtMapId.Text),
                    X = int.Parse(txtX.Text),
                    Y = int.Parse(txtY.Text)
                };

                if (isEditing)
                {
                    PointListForm.PointService.Update(Map, EditIndex, point);
                }
                else
                {
                    PointListForm.PointService.Add(Map, point);
                }

                PointListForm.RepopulateData();
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

        private void CreatePointObject_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (isEditing)
            {
                CreateAndExit(sender, e);
            }
        }
    }
}
