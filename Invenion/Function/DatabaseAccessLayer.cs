using System;
using System.Data;
using System.Data.SqlClient;
using Microsoft.Data;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Xml.Linq;

using Invenion.Models;

namespace Invenion.Function
{
    public class DatabaseAccessLayer
    {
        private readonly string connectionString;

        public DatabaseAccessLayer()
        {
            // Your SQL Server connection string
            connectionString = @"Data Source=LAPTOP-1TMIIIGV\SQLEXPRESS;Initial Catalog=INVENION_DB;Integrated Security=True;TrustServerCertificate=True";
        }

        // Method to get connection string - this is what your controller is looking for
        public string GetConnectionString()
        {
            return connectionString;
        }

    }

}