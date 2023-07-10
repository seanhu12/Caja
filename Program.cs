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
        static int numeroCaja;
        static int sucursal;

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

                Console.WriteLine("\nIngrese el número de la caja:");
                var numeroCajaStr = Console.ReadLine();
                if (!int.TryParse(numeroCajaStr, out numeroCaja))
                {
                    Console.WriteLine("Número de caja no válido.");
                    return;
                }

                Console.WriteLine("\nIngrese el Id de la sucursal:");
                var sucursalStr = Console.ReadLine();
                if (!int.TryParse(sucursalStr, out sucursal))
                {
                    Console.WriteLine("ID no válido.");
                    return;
                }

                MostrarMenu(channel);

                Console.WriteLine("Presiona Enter para cerrar la aplicación...");

                while (Console.ReadKey().Key != ConsoleKey.Escape) { }
            }
        }

        static void MostrarMenu(IModel channel)
        {
            while (true)
            {
                Console.WriteLine("\nSeleccione una opción:");
                Console.WriteLine("1. Ver productos disponibles");
                Console.WriteLine("2. Realizar una compra");
                Console.WriteLine("3. Salir");

                var seleccion = Console.ReadLine();

                switch (seleccion)
                {
                    case "1":
                        MostrarProductosDisponibles();
                        break;
                    case "2":
                        RealizarCompra(channel);
                        break;
                    case "3":
                        return;
                    default:
                        Console.WriteLine("Opción no válida. Por favor, intente nuevamente.");
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
            var productosSeleccionados = new Dictionary<string, ProductoCantidad>();
            int total = 0;

            while (true)
            {
                Console.WriteLine("\nIngrese el nombre del producto que desea comprar o \"Terminar\" para finalizar la compra:");
                var seleccion = Console.ReadLine();

                if (seleccion.ToLower() == "terminar")
                {
                    Console.WriteLine("\nGracias por su compra! Aquí está su boleta:");
                    Console.WriteLine($"Número de caja: {numeroCaja}");
                    Console.WriteLine($"Sucursal: {sucursal}");
                    foreach (var producto in productosSeleccionados)
                    {
                        Console.WriteLine($"Producto: {producto.Key}, Cantidad: {producto.Value.Cantidad}");
                    }
                    Console.WriteLine($"Total: {total}");

                    var compra = new Compra
                    {
                        NumeroCaja = numeroCaja,
                        Sucursal = sucursal,
                        Productos = productosSeleccionados,
                        Total = total,
                        Fecha = DateTime.Now
                    };

                    var productosSeleccionadosMessage = JsonConvert.SerializeObject(compra);
                    var body = Encoding.UTF8.GetBytes(productosSeleccionadosMessage);

                    channel.BasicPublish(exchange: "",
                                         routingKey: "productos_seleccionados",
                                         basicProperties: null,
                                         body: body);

                    Console.WriteLine("Datos de la compra enviados a Inventario.");
                    break;
                }

                var productoSeleccionado = ProductosDisponibles.Find(p => p.Nombre.ToLower() == seleccion.ToLower());
                if (productoSeleccionado != null)
                {
                    Console.WriteLine("Ingrese la cantidad que desea comprar de este producto:");
                    var cantidadStr = Console.ReadLine();
                    int cantidad;
                    if (!int.TryParse(cantidadStr, out cantidad))
                    {
                        Console.WriteLine("Cantidad no válida. Por favor, intente nuevamente.");
                        continue;
                    }

                    if (productosSeleccionados.ContainsKey(productoSeleccionado.Nombre))
                    {
                        productosSeleccionados[productoSeleccionado.Nombre].Cantidad += cantidad;
                    }
                    else
                    {
                        productosSeleccionados.Add(productoSeleccionado.Nombre, new ProductoCantidad { Producto = productoSeleccionado, Cantidad = cantidad });
                    }

                    total += productoSeleccionado.Precio * cantidad;
                    Console.WriteLine($"Producto agregado! Total actual: {total}");
                }
                else
                {
                    Console.WriteLine("Producto no encontrado. Por favor, intente nuevamente.");
                }
            }
        }
    }

    class Producto
    {
        public string Nombre { get; set; }
        public int Precio { get; set; }
    }

    class ProductoCantidad
    {
        public Producto Producto { get; set; }
        public int Cantidad { get; set; }
    }

    class Compra
    {
        public int NumeroCaja { get; set; }
        public int Sucursal { get; set; }
        public Dictionary<string, ProductoCantidad> Productos { get; set; }
        public int Total { get; set; }
        public DateTime Fecha { get; set; }
    }
}
