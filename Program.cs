using System;
using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Newtonsoft.Json;

namespace Caja
{
    class Program
    {
        static List<Producto> ProductosDisponibles = new List<Producto>();

        static void Main(string[] args)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "productos_disponibles",
                                     durable: false,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);

                channel.QueueDeclare(queue: "productos_seleccionados",
                                     durable: false,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);

                var productosDisponiblesConsumer = new EventingBasicConsumer(channel);
                productosDisponiblesConsumer.Received += (model, ea) =>
                {
                    var message = Encoding.UTF8.GetString(ea.Body.ToArray());
                    ProductosDisponibles = JsonConvert.DeserializeObject<List<Producto>>(message);
                };

                channel.BasicConsume(queue: "productos_disponibles",
                                     autoAck: true,
                                     consumer: productosDisponiblesConsumer);

                MostrarMenu(channel);

                Console.WriteLine("Presiona Enter para cerrar la aplicación...");

                while (Console.ReadKey().Key != ConsoleKey.Escape) { }
            }
        }

        static void MostrarMenu(IModel channel)
        {
            while (true)
            {
                Console.WriteLine("\n==== Menú ====");
                Console.WriteLine("1. Mostrar productos disponibles");
                Console.WriteLine("2. Realizar una compra");
                Console.WriteLine("3. Salir");

                Console.WriteLine("Ingrese el número de la opción deseada:");
                var opcionStr = Console.ReadLine();
                int opcion;
                if (!int.TryParse(opcionStr, out opcion))
                {
                    Console.WriteLine("Opción no válida.");
                    continue;
                }

                switch (opcion)
                {
                    case 1:
                        MostrarProductosDisponibles();
                        break;
                    case 2:
                        RealizarCompra(channel);
                        break;
                    case 3:
                        Console.WriteLine("Hasta luego!");
                        return;
                    default:
                        Console.WriteLine("Opción no válida.");
                        break;
                }
            }
        }

        static void MostrarProductosDisponibles()
        {
            Console.WriteLine("\nProductos disponibles:");
            foreach (var producto in ProductosDisponibles)
            {
                Console.WriteLine($"Nombre: {producto.Nombre}, Precio: {producto.Precio}");
            }
        }

        static void RealizarCompra(IModel channel)
        {
            Console.WriteLine("\nIngrese el nombre de la sucursal donde se realiza la compra:");
            var sucursal = Console.ReadLine();

            var productosSeleccionados = new Dictionary<string, int>();
            int total = 0;

            while (true)
            {
                Console.WriteLine("\nSeleccione un producto para agregar a la boleta (escribe 'terminar' para finalizar la compra):");
                var seleccion = Console.ReadLine();

                if (seleccion.ToLower() == "terminar")
                {
                    Console.WriteLine("\nGracias por su compra! Aquí está su boleta:");
                    Console.WriteLine($"Sucursal: {sucursal}");
                    foreach (var producto in productosSeleccionados)
                    {
                        Console.WriteLine($"Producto: {producto.Key}, Cantidad: {producto.Value}");
                    }
                    Console.WriteLine($"Total: {total}");

                    var compra = new Compra
                    {
                        Sucursal = sucursal,
                        Fecha = DateTime.Now,
                        Productos = productosSeleccionados,
                        Total = total
                    };

                    var productosSeleccionadosMessage = JsonConvert.SerializeObject(compra);
                    var body = Encoding.UTF8.GetBytes(productosSeleccionadosMessage);

                    channel.BasicPublish(exchange: "",
                                         routingKey: "productos_seleccionados",
                                         basicProperties: null,
                                         body: body);

                    Console.WriteLine("Productos seleccionados enviados a Inventario.");
                    break;
                }

                var productoSeleccionado = ProductosDisponibles.Find(p => p.Nombre == seleccion);
                if (productoSeleccionado != null)
                {
                    Console.WriteLine("Ingrese la cantidad que desea comprar:");
                    var cantidadStr = Console.ReadLine();
                    int cantidad;
                    if (!int.TryParse(cantidadStr, out cantidad))
                    {
                        Console.WriteLine("Cantidad no válida. Inténtalo de nuevo.");
                        continue;
                    }

                    if (productosSeleccionados.ContainsKey(productoSeleccionado.Nombre))
                    {
                        productosSeleccionados[productoSeleccionado.Nombre] += cantidad;
                    }
                    else
                    {
                        productosSeleccionados.Add(productoSeleccionado.Nombre, cantidad);
                    }

                    total += productoSeleccionado.Precio * cantidad;
                    Console.WriteLine($"Producto {productoSeleccionado.Nombre} agregado a la boleta.");
                }
                else
                {
                    Console.WriteLine("Producto no disponible. Inténtalo de nuevo.");
                }
            }
        }
    }

    class Producto
    {
        public string Nombre { get; set; }
        public int Precio { get; set; }
    }

    class Compra
    {
        public string Sucursal { get; set; }
        public DateTime Fecha { get; set; }
        public Dictionary<string, int> Productos { get; set; }
        public int Total { get; set; }
    }
}
